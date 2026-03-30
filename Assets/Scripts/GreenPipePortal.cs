using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public enum GreenPipePortalRole
{
    Entrance,
    Exit
}

/// <summary>
/// 녹색 파이프: standSurface 위에서 S/↓ 시 워프.
/// 입구: Entrance Warp Point = 출구→입구 워프 후 도달. 출구: Exit Warp Point = 입구→출구 워프 후 도달.
/// </summary>
[DisallowMultipleComponent]
public class GreenPipePortal : MonoBehaviour
{
    [Header("연결 · 워프 후 도달 위치")]
    [SerializeField] private GreenPipePortalRole role = GreenPipePortalRole.Entrance;
    [Tooltip("Entrance 전용: 출구에서 입구로 워프한 뒤 입구에 도달할 위치. 비우면 stand(밟는 면) 캐시.")]
    [SerializeField] private Transform entranceWarpPoint;
    [Tooltip("Exit 전용: 입구에서 출구로 워프한 뒤 출구에 도달할 위치. 비우면 PipeExitStand 등 캐시.")]
    [FormerlySerializedAs("exitPoint")]
    [SerializeField] private Transform exitWarpPoint;
    [Tooltip("‘아래로 빨려 들어갈’ 연출의 끝 높이(Y)만 사용. X/Z는 플레이어 위치에 고정(옆으로 끌리지 않음). 비우면 slideInDepth 만큼 아래.")]
    [SerializeField] private Transform entranceSlideEnd;
    [Tooltip("Entrance: 출구에서 올라올 때 시작 Y만(선택). Exit: 입구로 돌아올 때 시작 Y만(선택). X/Z는 도착 스탠드에 맞춤.")]
    [SerializeField] private Transform exitSlideStart;

    [Header("입력·조건")]
    [Tooltip("밟는 면(비트리거). 비우면 루트 기준 첫 비트리거 아닌 Collider2D")]
    [SerializeField] private Collider2D standSurface;
    [SerializeField] private float standCheckRayLength = 0.25f;
    [SerializeField] private bool requireGrounded = true;
    [SerializeField] private bool requireDownInput = true;

    [Header("연출(초)")]
    [SerializeField] private float slideInDuration = 0.4f;
    [SerializeField] private float travelHoldDuration = 0.12f;
    [SerializeField] private float slideInDepth = 3.4f;
    [SerializeField] private float emergeFromBelowOffset = 0f;

    [Header("기타")]
    [SerializeField] private float warpCooldown = 0.35f;

    public GreenPipePortalRole Role => role;

    private bool _warping;
    private float _cooldownUntil;

    static PlayerController s_resolvedPlayer;
    static int s_resolveFrame = -1;

    public Transform GetStandTransform() => standSurface != null ? standSurface.transform : transform;

    /// <summary>입구 역할: 출구→입구 워프 후 입구 도달(entranceWarpPoint 우선, 없으면 stand).</summary>
    public Transform GetEntranceLandingTransformForPairing()
    {
        if (role != GreenPipePortalRole.Entrance) return GetStandTransform();
        if (entranceWarpPoint != null) return entranceWarpPoint;
        return GetStandTransform();
    }

    /// <summary>출구 역할: 입구→출구 워프 후 출구 도달(exitWarpPoint 우선, 없으면 PipeExitStand / 루트).</summary>
    public Transform GetExitLandingTransformForPairing()
    {
        if (role != GreenPipePortalRole.Exit) return GetStandTransform();
        if (exitWarpPoint != null) return exitWarpPoint;
        var ps = GetComponent<PipeExitStand>();
        if (ps != null) return ps.GetStandPoint();
        return transform;
    }

    static PlayerController ResolvePlayerThisFrame()
    {
        if (s_resolveFrame != Time.frameCount)
        {
            s_resolveFrame = Time.frameCount;
            s_resolvedPlayer = Object.FindFirstObjectByType<PlayerController>();
        }
        return s_resolvedPlayer;
    }

    void Awake()
    {
        if (standSurface == null)
        {
            foreach (var c in transform.root.GetComponentsInChildren<Collider2D>(true))
            {
                if (c != null && !c.isTrigger)
                {
                    standSurface = c;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (_warping) return;
        if (Time.unscaledTime < _cooldownUntil) return;
        if (standSurface == null) return;

        var player = ResolvePlayerThisFrame();
        if (player == null || player.IsDead || player.IsInPipeWarp) return;
        if (!ArePlayerFeetOnStandSurface(player)) return;
        if (requireGrounded && !player.IsGrounded) return;
        if (requireDownInput && !ReadDownPressed()) return;

        if (role == GreenPipePortalRole.Exit)
        {
            if (!TryResolveEntrance(out Transform entranceStand))
                return;
            StartCoroutine(WarpRoutine(player, entranceStand));
        }
        else
        {
            if (!TryResolveExit(out Transform exitStand))
                return;
            StartCoroutine(WarpRoutine(player, exitStand));
        }
    }

    bool TryResolveExit(out Transform exitStand)
    {
        var entranceMarker = GetComponent<PlacedPipeEntranceEditMarker>()
            ?? GetComponentInParent<PlacedPipeEntranceEditMarker>();
        if (entranceMarker == null || string.IsNullOrEmpty(entranceMarker.pairId))
        {
            Debug.LogWarning($"[GreenPipePortal] 입구 pairId 없음: {gameObject.name}.");
            exitStand = null;
            return false;
        }

        if (PipePairCache.TryGetExitStand(entranceMarker.pairId, out exitStand))
            return true;

        Debug.LogWarning($"[GreenPipePortal] pairId={entranceMarker.pairId} 출구를 찾지 못했습니다: {gameObject.name}");
        exitStand = null;
        return false;
    }

    bool TryResolveEntrance(out Transform entranceStand)
    {
        var exitMarker = GetComponent<PlacedPipeExitEditMarker>()
            ?? GetComponentInParent<PlacedPipeExitEditMarker>();
        if (exitMarker == null || string.IsNullOrEmpty(exitMarker.pairId))
        {
            Debug.LogWarning($"[GreenPipePortal] 출구 pairId 없음: {gameObject.name}.");
            entranceStand = null;
            return false;
        }

        if (PipePairCache.TryGetEntranceStand(exitMarker.pairId, out entranceStand))
            return true;

        Debug.LogWarning($"[GreenPipePortal] pairId={exitMarker.pairId} 입구를 찾지 못했습니다: {gameObject.name}");
        entranceStand = null;
        return false;
    }

    bool ArePlayerFeetOnStandSurface(PlayerController player)
    {
        var pcol = player.GetComponent<Collider2D>() ?? player.GetComponentInParent<Collider2D>();
        if (pcol == null) return false;

        float footY = pcol.bounds.min.y;
        Vector2 origin = new Vector2(pcol.bounds.center.x, footY + 0.06f);
        var hits = Physics2D.RaycastAll(origin, Vector2.down, standCheckRayLength);
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider == standSurface)
                return true;
        }
        return false;
    }

    private static bool ReadDownPressed()
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        return kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
    }

    /// <summary>
    /// 입구→출구는 Exit Warp Point(자식 등)로 발 높이가 맞는 경우가 많고,
    /// 출구→입구는 standSurface.transform.position만 쓰면 콜라이더 중심이라 bounds와 어긋나 물리가 옆으로 밀 수 있음 → Collider2D가 있으면 윗면 중심 사용.
    /// </summary>
    static Vector3 GetWarpLandingWorldPosition(Transform t)
    {
        if (t == null) return Vector3.zero;
        var col = t.GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            var b = col.bounds;
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }
        return t.position;
    }

    private IEnumerator WarpRoutine(PlayerController player, Transform destinationStand)
    {
        _warping = true;
        var tr = player.transform;
        var startPos = tr.position;

        float slideEndY = entranceSlideEnd != null
            ? entranceSlideEnd.position.y
            : startPos.y - slideInDepth;
        // 파이프로 ‘아래로 들어가기’는 세로만 (entranceSlideEnd X가 어긋나면 출구→입구 시 옆으로 미끄러짐)
        Vector3 slideEnd = new Vector3(startPos.x, slideEndY, startPos.z);

        player.BeginPipeWarp();

        if (slideInDuration > 0.001f)
        {
            float t = 0f;
            while (t < slideInDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / slideInDuration);
                float y = Mathf.Lerp(startPos.y, slideEnd.y, u);
                tr.position = new Vector3(startPos.x, y, startPos.z);
                yield return null;
            }
        }
        else
            tr.position = slideEnd;

        float slideInDistance = Mathf.Abs(startPos.y - slideEnd.y);
        if (slideInDistance < 0.0001f)
            slideInDistance = slideInDepth;
        float slideInSpeed = slideInDistance / Mathf.Max(0.0001f, slideInDuration);

        yield return new WaitForSeconds(travelHoldDuration);

        Vector3 stand = GetWarpLandingWorldPosition(destinationStand);
        float emergeY = exitSlideStart != null
            ? exitSlideStart.position.y
            : stand.y - emergeFromBelowOffset;
        Vector3 emergeStart = new Vector3(stand.x, emergeY, stand.z);
        Vector3 endStand = stand;

        tr.position = emergeStart;

        float verticalTravel = Mathf.Abs(endStand.y - emergeStart.y);
        float slideOutDuration = verticalTravel / Mathf.Max(0.0001f, slideInSpeed);

        if (slideOutDuration > 0.001f)
        {
            float t = 0f;
            float y0 = emergeStart.y;
            float y1 = endStand.y;
            while (t < slideOutDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / slideOutDuration);
                float y = Mathf.Lerp(y0, y1, u);
                tr.position = new Vector3(stand.x, y, stand.z);
                yield return null;
            }
        }
        else
            tr.position = endStand;

        player.EndPipeWarp();

        var cam = FindFirstObjectByType<CameraFollow>();
        if (cam != null)
            cam.SnapToTarget();

        _cooldownUntil = Time.unscaledTime + warpCooldown;
        _warping = false;
    }
}
