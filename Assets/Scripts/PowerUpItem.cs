using System.Collections;
using UnityEngine;

/// <summary>
/// 버섯/꽃 아이템. Edit 씬의 맵 편집 모드에서는 정지, Test Play·Play 씬에서만 이동/상승.
/// Collider2D 비트리거 — OnCollisionEnter2D로 픽업·벽 반전.
/// </summary>
public class PowerUpItem : MonoBehaviour
{
    [SerializeField] private PowerUpType type = PowerUpType.Mushroom;

    [Header("Mushroom — 한 방향 이동 + 중력")]
    [SerializeField] private float moveSpeed = 4f;
    [Tooltip("-1 = 왼쪽, 1 = 오른쪽")]
    [SerializeField] private float moveDirection = -1f;
    [Tooltip("Physics2D 중력 배율. 0이면 떨어지지 않음.")]
    [SerializeField] private float mushroomGravityScale = 1f;
    [Tooltip("발밑 검사 박스 높이(타일 경계에서 레이 깜빡임 방지).")]
    [SerializeField] private float groundFootHeight = 0.12f;
    [Tooltip("발밑 검사 박스 가로 = 콜라이더 너비 × 이 값.")]
    [SerializeField] private float groundFootWidthScale = 0.92f;
    [Tooltip("바닥 판정이 잠깐 끊겨도 이 프레임 동안은 여전히 '착지'로 간주.")]
    [SerializeField] private int groundedCoyoteFrames = 4;

    [Header("Flower — 바닥에서 상승 후 정지")]
    [SerializeField] private float riseDistance = 0.8f;
    [SerializeField] private float riseDuration = 0.35f;

    private Rigidbody2D rb;
    private Collider2D col2D;
    private int _groundedCoyote;
    private bool consumed;
    private bool gameplayStarted;
    /// <summary>물음표 블록에서 막 스폰된 버섯 — BeginGameplayFirstTime에서 일반 버섯 물리 적용을 건너뜀.</summary>
    private bool blockSpawnScheduled;
    /// <summary>블록에서 올라오는 연출 중에는 수평 이동·착지 판정을 하지 않음.</summary>
    private bool mushroomBlockRising;
    /// <summary>직전 프레임까지 맵 편집 모드였는지. 첫 프레임에서 Play/Test 진입 감지용으로 true로 둠.</summary>
    private bool wasMapEditMode = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col2D = GetComponent<Collider2D>();
    }

    private void Start()
    {
        if (GameState.IsMapEditMode)
            FreezePhysicsForEdit();
    }

    private void Update()
    {
        if (GameState.IsMapEditMode)
        {
            StopAllCoroutines();
            FreezePhysicsForEdit();
            wasMapEditMode = true;
            return;
        }

        // 맵 편집 종료 직후(Test Play 시작·Play 씬 등)
        if (wasMapEditMode)
        {
            wasMapEditMode = false;
            if (gameplayStarted)
                ResumeGameplayAfterLeavingEditMode();
            else
            {
                gameplayStarted = true;
                BeginGameplayFirstTime();
            }
        }
    }

    private void FreezePhysicsForEdit()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    /// <summary>Test Play를 다시 켠 경우 — 꽃 상승은 반복하지 않고 버섯만 물리 재개.</summary>
    private void ResumeGameplayAfterLeavingEditMode()
    {
        if (type == PowerUpType.Mushroom)
            ApplyMushroomPhysics();
    }

    private void BeginGameplayFirstTime()
    {
        if (type == PowerUpType.Mushroom && blockSpawnScheduled)
        {
            gameplayStarted = true;
            return;
        }

        if (type == PowerUpType.Flower && blockSpawnScheduled)
        {
            gameplayStarted = true;
            return;
        }

        if (type == PowerUpType.Flower)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            StartCoroutine(FlowerRiseRoutine());
        }
        else
            ApplyMushroomPhysics();
    }

    /// <summary>
    /// 물음표 블록에서 스폰 직후 호출. 버섯은 올라온 뒤 걷기, 꽃은 올라온 뒤 정지(기존 꽃과 동일).
    /// </summary>
    public void BeginReleaseFromBlock(Vector3 endWorldPosition, float riseDistance, float riseDuration)
    {
        blockSpawnScheduled = true;

        if (type == PowerUpType.Mushroom)
        {
            mushroomBlockRising = true;
            EnsureMushroomRigidbody();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            StartCoroutine(MushroomBlockRiseRoutine(endWorldPosition, riseDistance, riseDuration));
        }
        else if (type == PowerUpType.Flower)
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            StartCoroutine(FlowerBlockRiseRoutine(endWorldPosition, riseDistance, riseDuration));
        }
    }

    private IEnumerator FlowerBlockRiseRoutine(Vector3 endPos, float riseDistance, float riseDuration)
    {
        Vector3 startPos = endPos + Vector3.down * riseDistance;
        transform.position = startPos;
        if (rb != null)
            rb.MovePosition(startPos);

        float dur = Mathf.Max(0.01f, riseDuration);
        float t = 0f;
        while (t < 1f)
        {
            if (GameState.IsMapEditMode) yield break;

            t += Time.deltaTime / dur;
            float u = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, u));
            transform.position = p;
            if (rb != null)
                rb.MovePosition(p);
            yield return null;
        }

        transform.position = endPos;
        if (rb != null)
        {
            rb.MovePosition(endPos);
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private IEnumerator MushroomBlockRiseRoutine(Vector3 endPos, float riseDistance, float riseDuration)
    {
        Vector3 startPos = endPos + Vector3.down * riseDistance;
        transform.position = startPos;
        if (rb != null)
            rb.MovePosition(startPos);

        float dur = Mathf.Max(0.01f, riseDuration);
        float t = 0f;
        while (t < 1f)
        {
            if (GameState.IsMapEditMode) yield break;

            t += Time.deltaTime / dur;
            float u = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, u));
            transform.position = p;
            if (rb != null)
                rb.MovePosition(p);
            yield return null;
        }

        transform.position = endPos;
        if (rb != null)
        {
            rb.MovePosition(endPos);
            rb.linearVelocity = Vector2.zero;
        }

        mushroomBlockRising = false;
        ApplyMushroomPhysics();
    }

    /// <summary>버섯: Dynamic + 중력 + Y이동 허용(바닥으로 떨어짐).</summary>
    private void ApplyMushroomPhysics()
    {
        EnsureMushroomRigidbody();
        if (rb == null)
        {
            Debug.LogWarning("[PowerUpItem] Mushroom needs Rigidbody2D to fall. Add Rigidbody2D on the prefab.");
            return;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        rb.gravityScale = mushroomGravityScale;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
    }

    void EnsureMushroomRigidbody()
    {
        if (type != PowerUpType.Mushroom) return;
        if (rb != null) return;
        rb = gameObject.AddComponent<Rigidbody2D>();
    }

    private IEnumerator FlowerRiseRoutine()
    {
        Vector3 endPos = transform.position;
        Vector3 startPos = endPos + Vector3.down * riseDistance;
        transform.position = startPos;
        if (rb != null)
            rb.MovePosition(startPos);

        float t = 0f;
        while (t < 1f)
        {
            if (GameState.IsMapEditMode) yield break;

            t += Time.deltaTime / Mathf.Max(0.01f, riseDuration);
            float u = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, u));
            transform.position = p;
            if (rb != null)
                rb.MovePosition(p);
            yield return null;
        }

        transform.position = endPos;
        if (rb != null)
        {
            rb.MovePosition(endPos);
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        if (GameState.IsMapEditMode) return;
        if (type != PowerUpType.Mushroom) return;
        if (mushroomBlockRising) return;

        float dir = Mathf.Sign(moveDirection);
        if (dir == 0f) dir = -1f;

        if (rb != null)
        {
            bool grounded = IsMushroomGroundedStable();
            var v = rb.linearVelocity;
            v.x = grounded ? dir * moveSpeed : 0f;
            rb.linearVelocity = v;
            if (rb.gravityScale != mushroomGravityScale)
                rb.gravityScale = mushroomGravityScale;
        }
        else
        {
            transform.position += (Vector3)(Vector2.right * (dir * moveSpeed * Time.fixedDeltaTime));
        }
    }

    /// <summary>발밑 박스로 바닥 검사 + 코요테로 프레임 간 깜빡임 완화.</summary>
    private bool IsMushroomGroundedStable()
    {
        bool raw = IsMushroomGroundedOverlap();
        if (raw)
            _groundedCoyote = groundedCoyoteFrames;
        else if (_groundedCoyote > 0)
            _groundedCoyote--;

        return _groundedCoyote > 0;
    }

    private bool IsMushroomGroundedOverlap()
    {
        if (col2D == null) col2D = GetComponent<Collider2D>();
        if (col2D == null) return false;

        Bounds b = col2D.bounds;
        float w = Mathf.Max(b.size.x * groundFootWidthScale, 0.06f);
        float h = Mathf.Max(groundFootHeight, 0.05f);
        // 발밑 살짝 아래까지 박스를 깔아 타일 경계·틈에서 레이보다 안정적
        Vector2 center = new Vector2(b.center.x, b.min.y - h * 0.5f + 0.02f);
        var hits = Physics2D.OverlapBoxAll(center, new Vector2(w, h), 0f, Physics2D.DefaultRaycastLayers);

        foreach (var hit in hits)
        {
            if (hit == null || hit == col2D) continue;
            if (hit.transform.root == transform.root) continue;
            if (hit.GetComponentInParent<PlayerController>() != null) continue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 플레이어 콜라이더가 Trigger이면 충돌 콜백은 오지 않고 Trigger만 온다. 둘 다 처리.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPickupPlayer(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (TryPickupPlayer(collision.collider))
            return;

        if (GameState.IsMapEditMode) return;

        if (type != PowerUpType.Mushroom) return;
        if (mushroomBlockRising) return;
        if (collision.contactCount == 0) return;

        Vector2 n = collision.GetContact(0).normal;
        if (Mathf.Abs(n.x) > 0.4f)
            moveDirection *= -1f;
    }

    /// <returns>플레이어를 먹었으면 true (벽 반전 처리 안 함)</returns>
    private bool TryPickupPlayer(Collider2D other)
    {
        if (consumed) return false;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null && other.attachedRigidbody != null)
            player = other.attachedRigidbody.GetComponent<PlayerController>();
        if (player == null)
            player = other.transform.root.GetComponentInChildren<PlayerController>(true);

        if (player == null) return false;
        return TryConsumeByPlayer(player);
    }

    /// <summary>PlayerController 쪽 OnCollision/OnTrigger에서도 호출 — 한쪽만 콜백 오는 경우 대비.</summary>
    public bool TryConsumeByPlayer(PlayerController player)
    {
        if (consumed || player == null) return false;
        consumed = true;
        player.PowerUp(type);
        Destroy(gameObject);
        return true;
    }
}
