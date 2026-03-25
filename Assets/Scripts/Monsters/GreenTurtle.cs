using UnityEngine;
using UnityEngine.Serialization;
/// <summary>
/// 녹색 엉금엉금. Animator: 평시 Walk, 밟힘 직후 짧은 Sliding(껍데기에서 밀려 나옴), 이후 InnerWalk(알맹이 걷기).
/// <b>isSliding</b> / <b>isInnerWalk</b> Bool로 애니 전환.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class GreenTurtle : MonoBehaviour
{
    /// <summary>게임플레이 상태. Animator의 Walk / Sliding / InnerWalk와 1:1 대응.</summary>
    /// <remarks>값(0,1,2)은 예전 Walking/Shell/ShellMoving과 동일해 저장된 프리팹과 호환됩니다.</remarks>
    public enum State
    {
        Walk = 0,
        InnerWalk = 1,
        Sliding = 2
    }

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [Tooltip("알맹이(InnerWalk) 때 좌우 이동 속도")]
    [SerializeField] private float innerWalkSpeed = 2f;
    [Tooltip("Sliding·스폰 껍데기 가로 속도. 보통 walkSpeed의 약 2배로 맞춤")]
    [SerializeField] private float slideSpeed = 4f;
    [Tooltip("Walk에서 밟혀 Sliding에 들어간 뒤, 이 시간이 지나면 InnerWalk로 전환")]
    [SerializeField] private float slideDuration = 1f;
    [SerializeField] private float stompBounce = 8f;

    [Header("InnerWalk 밟힘 → 사망 연출")]
    [Tooltip("알맹이가 밟히면 위로 튀는 초기 세로 속도. 아주 낮게 두려면 1~3 정도.")]
    [SerializeField] private float innerWalkDeathPop = 2.5f;
    [Tooltip("사망 중에만 적용할 Rigidbody2D.gravityScale 배율. 클수록 빨리 낙하.")]
    [SerializeField] private float innerWalkDeathGravityScale = 2.5f;
    [Tooltip("이 시간(초) 동안은 바닥 닿아도 Destroy 안 함 (바로 붙어 있을 때 즉사 방지)")]
    [SerializeField] private float innerWalkDeathAirTime = 0.12f;
    [Tooltip("이 이하의 세로 속도로 바닥에 닿으면 착지로 보고 Destroy")]
    [SerializeField] private float innerWalkDeathLandVy = 0.5f;

    [Header("Shell (첫 밟힘 시 그 자리에 스폰)")]
    [Tooltip("비어 있으면 껍데기 오브젝트를 만들지 않음")]
    [FormerlySerializedAs("stompSpawnPrefab")]
    [SerializeField] private GameObject shellPrefab;
    [Tooltip("스폰 부모. 비어 있으면 월드 좌표")]
    [FormerlySerializedAs("stompSpawnParent")]
    [SerializeField] private Transform shellSpawnParent;

    [Header("Animator")]
    [Tooltip("비어 있으면 자기 또는 자식에서 Animator 검색")]
    [SerializeField] private Animator animator;
    [SerializeField] private string paramIsSliding = "isSliding";
    [SerializeField] private string paramIsInnerWalk = "isInnerWalk";

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float stompThreshold = 0.2f;
    [Tooltip("원본 스프라이트가 오른쪽을 향하면 체크. 왼쪽이 기본(미체크)이면 flipX = (direction > 0).")]
    [SerializeField] private bool spriteFacesRightByDefault;

    private State currentState = State.Walk;
    private Rigidbody2D rb;
    private float direction = -1f;
    private float slideTimeRemaining;
    private ContactPoint2D[] slideContactBuffer = new ContactPoint2D[16];

    private bool isDyingFromInnerWalkStomp;
    private float innerWalkDeathStartedAt;

    /// <summary>Sliding에 들어간 프레임. 같은 프레임에 또 밟힘 처리되면 InnerWalk로 덮어써지는 것을 막음.</summary>
    private int slidingEnteredFrame = -1;

    [Tooltip("옆벽 반전 후 같은 접촉에서 매 프레임 뒤집히지 않도록 최소 간격(초)")]
    [SerializeField] private float sideWallFlipCooldown = 0.2f;
    private float lastSideWallFlipTime = -999f;

    [Header("Wall check (Walk / InnerWalk)")]
    [Tooltip("이동 방향으로 Cast할 거리. 모서리에서 접촉 법선만으로는 벽 판정이 빠질 때 사용")]
    [SerializeField] private float wallCheckCastDistance = 0.12f;
    private Collider2D bodyCollider;
    private readonly RaycastHit2D[] wallCastBuffer = new RaycastHit2D[8];

    public State CurrentState => currentState;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        ApplyAnimatorState();
        ApplyFacingFromDirection();
    }

    private void FixedUpdate()
    {
        if (GameState.IsVictory)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isDyingFromInnerWalkStomp)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        switch (currentState)
        {
            case State.Walk:
                TryFlipIfWallAhead();
                rb.linearVelocity = new Vector2(direction * walkSpeed, rb.linearVelocity.y);
                break;
            case State.InnerWalk:
                TryFlipIfWallAhead();
                rb.linearVelocity = new Vector2(direction * innerWalkSpeed, rb.linearVelocity.y);
                break;
            case State.Sliding:
            {
                float slideVx = slideSpeed;
                if (IsSlidingBlockedByLateralGround())
                    slideVx = 0f;
                rb.linearVelocity = new Vector2(direction * slideVx, rb.linearVelocity.y);
                slideTimeRemaining -= Time.fixedDeltaTime;
                if (slideTimeRemaining <= 0f)
                    SetState(State.InnerWalk);
                break;
            }
        }
    }

    /// <summary>Sliding 중 좌우 벽(지면의 수직 법선)에 막혀 있으면 true. 방향은 바꾸지 않고 가로 속도만 0.</summary>
    private bool IsSlidingBlockedByLateralGround()
    {
        int n = rb.GetContacts(slideContactBuffer);
        for (int i = 0; i < n; i++)
        {
            var c = slideContactBuffer[i];
            if (c.collider == null || !c.collider.CompareTag(groundTag)) continue;
            if (Mathf.Abs(c.normal.x) > 0.5f)
                return true;
        }
        return false;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (isDyingFromInnerWalkStomp && col.gameObject.CompareTag(groundTag))
        {
            if (Time.time >= innerWalkDeathStartedAt + innerWalkDeathAirTime
                && rb.linearVelocity.y <= innerWalkDeathLandVy)
            {
                foreach (var c in col.contacts)
                {
                    if (c.normal.y > 0.4f)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }
            }
        }

        if (isDyingFromInnerWalkStomp)
            return;

        if (GameState.IsVictory)
            return;

        // Walk / InnerWalk: 좌우 벽에 닿으면 이동 방향 반전 + 스프라이트 좌우 반전. Sliding은 여기서 다루지 않음.
        if (col.gameObject.CompareTag(groundTag)
            && (currentState == State.Walk || currentState == State.InnerWalk))
        {
            TryFlipOnSideWall(col);
        }

        if (currentState == State.Sliding && col.gameObject.CompareTag(enemyTag))
            Destroy(col.gameObject);

        if (col.gameObject.CompareTag(playerTag))
            HandlePlayerCollision(col);
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (isDyingFromInnerWalkStomp) return;
        if (GameState.IsVictory) return;

        // Enter만으로는 벽에 밀린 상태가 누락될 수 있음. Sliding은 벽 반전 없음.
        if (col.gameObject.CompareTag(groundTag)
            && (currentState == State.Walk || currentState == State.InnerWalk))
        {
            TryFlipOnSideWall(col);
        }
    }

    private void HandlePlayerCollision(Collision2D col)
    {
        if (isDyingFromInnerWalkStomp) return;

        var playerRb = col.gameObject.GetComponent<Rigidbody2D>();
        var playerController = col.gameObject.GetComponent<PlayerController>();

        bool isStomped = playerRb != null
            && playerRb.linearVelocity.y < 0
            && col.gameObject.transform.position.y > transform.position.y + stompThreshold;

        switch (currentState)
        {
            case State.Walk:
                if (isStomped)
                    Stomp(playerRb);
                else
                    playerController?.TakeDamage();
                break;

            case State.InnerWalk:
                if (isStomped)
                {
                    if (playerRb != null)
                        playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
                    BeginInnerWalkStompDeath(col.gameObject);
                }
                else
                    playerController?.TakeDamage();
                break;

            case State.Sliding:
                if (isStomped)
                {
                    // 같은 프레임에 플레이어 콜라이더 2개 등으로 Enter가 두 번 오면
                    // Sliding 직후 InnerWalk로 바뀌어 껍데기·슬라이드가 안 보이는 것처럼 됨.
                    if (Time.frameCount != slidingEnteredFrame)
                    {
                        SetState(State.InnerWalk);
                        if (playerRb != null)
                            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
                    }
                }
                else
                    playerController?.TakeDamage();
                break;
        }
    }

    private void Stomp(Rigidbody2D playerRb)
    {
        if (currentState != State.Walk) return;

        if (shellPrefab == null)
            Debug.LogWarning("[GreenTurtle] Shell prefab is not assigned. Assign Shell Prefab on the Green Turtle prefab or instance.");

        if (shellPrefab != null)
        {
            Transform parent = shellSpawnParent != null ? shellSpawnParent : null;
            float footY = GetSelfLowestWorldY();
            GameObject shell = Instantiate(shellPrefab, transform.position, Quaternion.identity, parent);
            AlignInstanceBottomToWorldY(shell, footY);
            if (shell.TryGetComponent<Rigidbody2D>(out var shellRb))
                shellRb.linearVelocity = new Vector2(direction * slideSpeed, shellRb.linearVelocity.y);
        }

        SetState(State.Sliding);
        if (playerRb != null)
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
    }

    void BeginInnerWalkStompDeath(GameObject playerGo)
    {
        if (isDyingFromInnerWalkStomp) return;
        isDyingFromInnerWalkStomp = true;
        innerWalkDeathStartedAt = Time.time;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.flipY = true;

        if (animator != null)
            animator.enabled = false;

        rb.gravityScale = innerWalkDeathGravityScale;
        rb.angularVelocity = 0f;
        rb.linearVelocity = new Vector2(0f, innerWalkDeathPop);

        Collider2D[] myCols = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] playerCols = playerGo.GetComponentsInChildren<Collider2D>(true);
        foreach (var my in myCols)
        {
            if (my == null || !my.enabled) continue;
            foreach (var pc in playerCols)
            {
                if (pc != null && pc.enabled)
                    Physics2D.IgnoreCollision(my, pc, true);
            }
        }
    }

    private void SetState(State state)
    {
        State prev = currentState;
        currentState = state;

        if (state == State.Sliding)
        {
            slideTimeRemaining = slideDuration;
            slidingEnteredFrame = Time.frameCount;
        }

        ApplyAnimatorState();
    }

    /// <summary>isSliding / isInnerWalk만 설정. 둘 다 false면 평시(Walk) 애니.</summary>
    private void ApplyAnimatorState()
    {
        if (animator == null) return;

        bool sliding = currentState == State.Sliding;
        bool innerWalk = currentState == State.InnerWalk;

        if (!string.IsNullOrEmpty(paramIsSliding))
            animator.SetBool(paramIsSliding, sliding);
        if (!string.IsNullOrEmpty(paramIsInnerWalk))
            animator.SetBool(paramIsInnerWalk, innerWalk);
    }

    /// <summary>
    /// 바닥·벽 모서리 등에서 접촉이 수평면으로만 잡혀 법선 기반 반전이 실패할 때,
    /// 이동 방향으로 짧게 Cast해 수직면(Ground)이면 방향 반전.
    /// </summary>
    void TryFlipIfWallAhead()
    {
        if (Time.time - lastSideWallFlipTime < sideWallFlipCooldown)
            return;
        if (bodyCollider == null) return;

        Vector2 moveDir = new Vector2(direction, 0f);
        if (Mathf.Abs(moveDir.x) < 0.01f) return;

        var filter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = Physics2D.DefaultRaycastLayers
        };

        int n = bodyCollider.Cast(moveDir, filter, wallCastBuffer, wallCheckCastDistance);
        for (int i = 0; i < n; i++)
        {
            var h = wallCastBuffer[i];
            if (h.collider == null || !h.collider.CompareTag(groundTag)) continue;
            // 수직 벽(또는 주로 옆면): 바닥만 맞으면 법선이 위쪽이라 스킵
            if (Mathf.Abs(h.normal.x) <= Mathf.Abs(h.normal.y)) continue;

            direction = -direction;
            ApplyFacingFromDirection();
            lastSideWallFlipTime = Time.time;
            return;
        }
    }

    /// <summary>지면 태그 충돌 중 벽(법선이 주로 좌우)이면 방향 반전. Sliding에서는 호출하지 않음.</summary>
    /// <remarks>
    /// Unity 접촉 법선은 항상 "거북 바깥"이 아닐 수 있어 direction*nx 검사는 빈번히 실패함.
    /// 상대 속도·법선 내적이 음수일 때만 접근 중으로 보고, 쿨다운으로 Stay 연속 반전을 막음.
    /// </remarks>
    void TryFlipOnSideWall(Collision2D col)
    {
        if (Time.time - lastSideWallFlipTime < sideWallFlipCooldown)
            return;

        foreach (var contact in col.contacts)
        {
            Vector2 n = contact.normal;
            if (Mathf.Abs(n.x) <= Mathf.Abs(n.y))
                continue;

            // 접촉면을 따라 서로 가까워지는 성분(음수면 접근 중)
            float approach = Vector2.Dot(col.relativeVelocity, n);
            if (approach >= -0.02f)
                continue;

            direction = -direction;
            ApplyFacingFromDirection();
            lastSideWallFlipTime = Time.time;
            break;
        }
    }

    /// <summary><see cref="direction"/>에 맞춰 스프라이트 좌우 반전.</summary>
    void ApplyFacingFromDirection()
    {
        bool flipX = spriteFacesRightByDefault
            ? direction < 0f
            : direction > 0f;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            sr.flipX = flipX;
        }
    }

    /// <summary>이 거북의 콜라이더/스프라이트 중 월드 Y 최소값(발 쪽).</summary>
    float GetSelfLowestWorldY()
    {
        float minY = float.MaxValue;
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
        {
            if (!c.enabled) continue;
            minY = Mathf.Min(minY, c.bounds.min.y);
        }
        if (minY < float.MaxValue) return minY;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || !sr.enabled) continue;
            minY = Mathf.Min(minY, sr.bounds.min.y);
        }
        return minY < float.MaxValue ? minY : transform.position.y;
    }

    /// <summary>인스턴스 하단이 <paramref name="worldFloorY"/>에 오도록 Y만 이동. 에디트 몬스터 배치와 동일한 기준.</summary>
    static void AlignInstanceBottomToWorldY(GameObject instance, float worldFloorY)
    {
        float minY = float.MaxValue;
        foreach (var col in instance.GetComponentsInChildren<Collider2D>(true))
        {
            if (!col.enabled) continue;
            minY = Mathf.Min(minY, col.bounds.min.y);
        }
        if (minY < float.MaxValue)
        {
            instance.transform.position += new Vector3(0f, worldFloorY - minY, 0f);
            return;
        }
        foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || !sr.enabled) continue;
            minY = Mathf.Min(minY, sr.bounds.min.y);
        }
        if (minY < float.MaxValue)
            instance.transform.position += new Vector3(0f, worldFloorY - minY, 0f);
    }
}
