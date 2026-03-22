using System.Collections;
using UnityEngine;

/// <summary>
/// 물음표 블록: 플레이어가 아래에서 맞추면 짧게 위로 튀고, 사용됨 스프라이트로 바뀌며 아이템을 스폰합니다.
/// 스몰→버섯, 빅·플라워→꽃만(꽃 프리팹 미지정 시 빅/플라워는 아이템 없음).
/// Solid 레이어 콜라이더 + SpriteRenderer가 있는 프리팹에 붙입니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class QuestionBlock : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [Tooltip("아이템이 남아 있을 때 (물음표)")]
    [SerializeField] private Sprite activeSprite;
    [Tooltip("사용된 뒤 (빈 블록)")]
    [SerializeField] private Sprite inactiveSprite;

    [Header("Bump")]
    [SerializeField] private float bumpHeight = 0.12f;
    [SerializeField] private float bumpHalfDuration = 0.08f;

    [Header("Item spawn")]
    [Tooltip("스몰 마리오일 때 스폰 (PowerUpType Mushroom 프리팹).")]
    [SerializeField] private GameObject mushroomPrefab;
    [Tooltip("빅·플라워 마리오일 때만 스폰 (PowerUpType Flower). 비어 있으면 해당 상태에서는 아이템 없음.")]
    [SerializeField] private GameObject flowerPrefab;
    [SerializeField] private float mushroomRiseDistance = 0.65f;
    [SerializeField] private float mushroomRiseDuration = 0.35f;
    [Tooltip("스폰 직후 아이템이 올라오는 동안 부모(에디트 루트)에 두지 않고 월드에 둘지.")]
    [SerializeField] private bool spawnMushroomAsWorldRoot = true;

    [Header("Item spawn position")]
    [Tooltip("비어 있으면 부모에서 Grid를 찾음. 있으면 블록이 있는 셀의 '위 칸' 중앙에 아이템이 정착.")]
    [SerializeField] private Grid gridForCellSnap;
    [Tooltip("Grid을 못 쓸 때: 블록 중심에서 위로 이 만큼이 아이템 목표 위치(타일 한 칸=1).")]
    [SerializeField] private float itemRestYOffsetIfNoGrid = 1f;

    private Collider2D col2D;
    private bool used;
    private bool bumping;

    private void Awake()
    {
        col2D = GetComponent<Collider2D>();
        if (targetSpriteRenderer == null)
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
        if (gridForCellSnap == null)
            gridForCellSnap = GetComponentInParent<Grid>();
        ApplyVisualState();
    }

    /// <summary>아이템이 올라와 멈출 위치 — 블록과 같은 칸이 아니라 바로 위 칸(또는 Y 오프셋).</summary>
    private Vector3 GetItemRestWorldPosition()
    {
        Vector3 blockCenter = transform.position;
        blockCenter.z = 0f;

        if (gridForCellSnap != null)
        {
            Vector3Int cell = gridForCellSnap.WorldToCell(blockCenter);
            cell.y += 1;
            Vector3 rest = gridForCellSnap.GetCellCenterWorld(cell);
            rest.z = 0f;
            return rest;
        }

        return blockCenter + Vector3.up * Mathf.Max(0.01f, itemRestYOffsetIfNoGrid);
    }

    private void ApplyVisualState()
    {
        if (targetSpriteRenderer == null) return;
        if (used && inactiveSprite != null)
            targetSpriteRenderer.sprite = inactiveSprite;
        else if (!used && activeSprite != null)
            targetSpriteRenderer.sprite = activeSprite;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (used || bumping || GameState.IsMapEditMode) return;

        var player = collision.collider.GetComponentInParent<PlayerController>();
        if (player == null) return;

        if (!IsHitFromBelow(collision, player))
            return;

        PlayerState stateAtHit = player.CurrentState;

        used = true;
        StartCoroutine(BumpAndSpawnRoutine(stateAtHit));
    }

    /// <summary>
    /// 아래에서 박았는지: 접촉점이 블록 하단부이고, 플레이어가 블록 중심보다 아래에 있음.
    /// </summary>
    private bool IsHitFromBelow(Collision2D collision, PlayerController player)
    {
        if (col2D == null) return false;

        Bounds b = col2D.bounds;
        float bottomBand = b.min.y + Mathf.Max(0.02f, b.size.y * 0.22f);

        bool contactOnBottom = false;
        int n = collision.contactCount;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = collision.GetContact(i).point;
            if (p.y <= bottomBand)
            {
                contactOnBottom = true;
                break;
            }
        }

        if (!contactOnBottom) return false;

        float py = player.transform.position.y;
        if (py >= b.center.y - 0.02f)
            return false;

        return true;
    }

    /// <summary>스몰 → 버섯만. 빅·플라워 → 꽃만(프리팹 미지정 시 스폰 없음).</summary>
    private static GameObject PickItemPrefab(PlayerState stateAtHit, GameObject mushroomPrefab, GameObject flowerPrefab)
    {
        switch (stateAtHit)
        {
            case PlayerState.Small:
                return mushroomPrefab;
            case PlayerState.Big:
            case PlayerState.Flower:
                if (flowerPrefab != null)
                    return flowerPrefab;
                Debug.LogWarning("[QuestionBlock] Flower prefab is not set; Big/Flower player gets no item from this block.");
                return null;
            default:
                return mushroomPrefab;
        }
    }

    private IEnumerator BumpAndSpawnRoutine(PlayerState stateAtHit)
    {
        bumping = true;
        Vector3 origin = transform.position;
        float upTime = Mathf.Max(0.01f, bumpHalfDuration);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / upTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = origin + Vector3.up * (bumpHeight * u);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / upTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = origin + Vector3.up * (bumpHeight * (1f - u));
            yield return null;
        }

        transform.position = origin;
        bumping = false;

        ApplyVisualState();

        GameObject prefab = PickItemPrefab(stateAtHit, mushroomPrefab, flowerPrefab);
        if (prefab != null)
        {
            Transform parent = spawnMushroomAsWorldRoot ? null : transform.parent;
            Vector3 itemRestPos = GetItemRestWorldPosition();
            var go = Instantiate(prefab, itemRestPos, Quaternion.identity, parent);

            var pu = go.GetComponent<PowerUpItem>();
            if (pu != null)
                pu.BeginReleaseFromBlock(itemRestPos, mushroomRiseDistance, mushroomRiseDuration);
        }
    }
}
