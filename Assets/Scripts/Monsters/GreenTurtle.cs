using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
/// <summary>
/// 녹색 엉금엉금. Animator: 평시 Walk, 밟힘 직후 짧은 Sliding(껍데기에서 밀려 나옴), 이후 InnerWalk(알맹이 걷기).
/// <b>isSliding</b> / <b>isInnerWalk</b> Bool로 애니 전환.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class GreenTurtle : MonoBehaviour, IShellKillable
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
    [Tooltip("Ground 무시 후 아래로 떨어진 뒤 이 시간(초) 뒤 Destroy (굼바 deathMinLifetime과 맞춤)")]
    [SerializeField] private float innerWalkDeathDestroyDelay = 3f;

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

    [Header("Debug (GreenTurtle only)")]
    [Tooltip("켜면 방향 전환 시마다 Console에 이유·이동기준 앞/뒤(등) 추정·접촉법선 등 출력. 확인 후 반드시 끄기.")]
    [SerializeField] private bool debugLogDirectionChanges;
    [Tooltip("켜면 쉘 생성 시 Console 로그 + Scene 뷰에 마지막 생성 위치 기즈모. 확인 후 끄기.")]
    [SerializeField] private bool debugShellSpawn;

    private State currentState = State.Walk;
    private Rigidbody2D rb;
    private float direction = -1f;
    private float slideTimeRemaining;
    private ContactPoint2D[] slideContactBuffer = new ContactPoint2D[16];

    private bool isDyingFromInnerWalkStomp;
    private Coroutine innerWalkDeathDestroyRoutine;

    /// <summary>Sliding에 들어간 프레임. 같은 프레임에 또 밟힘 처리되면 InnerWalk로 덮어써지는 것을 막음.</summary>
    private int slidingEnteredFrame = -1;

    Vector3 debugLastShellSpawnWorld;
    bool debugHasLastShellSpawn;

    [Tooltip("옆벽 반전 후 같은 접촉에서 매 프레임 뒤집히지 않도록 최소 간격(초)")]
    [SerializeField] private float sideWallFlipCooldown = 0.2f;
    private float lastSideWallFlipTime = -999f;

    [Header("Wall check (Walk / InnerWalk)")]
    [Tooltip("이동 방향으로 Cast할 거리. 모서리에서 접촉 법선만으로는 벽 판정이 빠질 때 사용")]
    [SerializeField] private float wallCheckCastDistance = 0.12f;
    private Collider2D bodyCollider;
    private readonly RaycastHit2D[] wallCastBuffer = new RaycastHit2D[8];
    private float lastEnemyBumpTime = -999f;
    /// <summary>Enter가 누락되거나(겹침) Stay만 올 때도 1회 튕김. 접촉이 끊길 때까지 같은 상대와는 재튕김 없음(Stay 매프레임 뒤집힘 방지).</summary>
    private readonly HashSet<int> enemyBumpPairInstanceIdsUntilExit = new HashSet<int>();
    [Tooltip("몬스터 튕김: 상대 Enemy 루트가 이동 방향 **앞**에 있을 때만 방향 반전. (상대X−내X)×direction > 이 값일 때만. 0이면 약간의 앞쪽 분리만 있어도 허용.")]
    [SerializeField] private float enemyBumpRequireFrontAlongX = 0.02f;

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
            if (c.collider == null || !IsGroundTaggedCollider(c.collider)) continue;
            if (Mathf.Abs(c.normal.x) > 0.5f)
                return true;
        }
        return false;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (isDyingFromInnerWalkStomp)
            return;

        if (GameState.IsVictory)
            return;

        // 플레이어는 태그가 자식 콜라이더에 없을 수 있어 먼저 처리
        if (IsPlayerCollision(col))
        {
            HandlePlayerCollision(col);
            return;
        }

        // Walk / InnerWalk: Ground 옆면(벽) 접촉 시 방향 반전. Sliding은 벽에서 반전 없음.
        if (IsGroundTaggedCollider(col.collider)
            && (currentState == State.Walk || currentState == State.InnerWalk))
        {
            TryFlipOnSideWall(col);
        }

        TryBumpEnemy(col, "Enter");

        if (currentState == State.Sliding && col.gameObject.CompareTag(enemyTag)
            && (col.collider == null || col.collider.GetComponentInParent<GreenTurtleShell>(true) == null))
            Destroy(col.gameObject);
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (isDyingFromInnerWalkStomp) return;
        if (GameState.IsVictory) return;

        // Enter만으로는 벽에 밀린 상태가 누락될 수 있음. Sliding은 벽 반전 없음.
        if (IsGroundTaggedCollider(col.collider)
            && (currentState == State.Walk || currentState == State.InnerWalk))
        {
            TryFlipOnSideWall(col);
        }

        // 몬스터끼리 밀집 시 Enter가 안 오는 경우(겹침·슬립)에도 1회 방향 전환
        TryBumpEnemy(col, "Stay");

        // Enter에서 contact가 비어 밟힘만 놓친 경우 보완 (Walk일 때만 Stomp, 데미지 호출 없음)
        if (currentState == State.Walk && IsPlayerCollision(col))
        {
            var prb = col.rigidbody != null
                ? col.rigidbody
                : col.collider != null ? col.collider.GetComponentInParent<Rigidbody2D>() : null;
            if (prb != null && prb.gameObject.CompareTag(playerTag)
                && IsPlayerStompFromAbove(col, prb.gameObject))
                Stomp(prb);
        }
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (!TryGetEnemyRootInstanceId(col, out int otherId))
            return;
        enemyBumpPairInstanceIdsUntilExit.Remove(otherId);
    }

    /// <summary>상대가 Enemy 루트(또는 껍데기 등 FindEnemyRoot)이면 인스턴스 ID 반환.</summary>
    bool TryGetEnemyRootInstanceId(Collision2D col, out int instanceId)
    {
        instanceId = 0;
        var root = MonsterEnemyContactBounce.FindEnemyRoot(col.gameObject, enemyTag);
        if (root == null || root == gameObject) return false;
        instanceId = root.GetInstanceID();
        return true;
    }

    void TryBumpEnemy(Collision2D col, string callbackPhase)
    {
        if (Time.time - lastEnemyBumpTime < MonsterEnemyContactBounce.CooldownSeconds) return;
        var root = MonsterEnemyContactBounce.FindEnemyRoot(col.gameObject, enemyTag);
        if (root == null || root == gameObject) return;
        int pairId = root.GetInstanceID();
        if (enemyBumpPairInstanceIdsUntilExit.Contains(pairId))
            return;
        if (currentState != State.Walk && currentState != State.InnerWalk && currentState != State.Sliding)
            return;

        // 3번: 앞에서 부딪혔을 때만 반전 (등/옆 겹침만으로는 반전하지 않음)
        float dx = root.transform.position.x - transform.position.x;
        float along = dx * direction;
        if (along <= enemyBumpRequireFrontAlongX)
            return;

        float dirBefore = direction;
        lastEnemyBumpTime = Time.time;
        enemyBumpPairInstanceIdsUntilExit.Add(pairId);
        direction = -direction;
        ApplyFacingFromDirection();
        if (debugLogDirectionChanges)
            DebugLogGreenTurtleFlip_EnemyBump(callbackPhase, dirBefore, direction, col, root);
    }

    private void HandlePlayerCollision(Collision2D col)
    {
        if (isDyingFromInnerWalkStomp) return;

        var playerRb = col.rigidbody != null
            ? col.rigidbody
            : col.gameObject.GetComponent<Rigidbody2D>()
              ?? col.gameObject.GetComponentInParent<Rigidbody2D>();
        var playerController = col.gameObject.GetComponent<PlayerController>()
            ?? col.gameObject.GetComponentInParent<PlayerController>();

        GameObject playerGo = playerRb != null ? playerRb.gameObject : col.gameObject;
        bool isStomped = IsPlayerStompFromAbove(col, playerGo);

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
                    BeginInnerWalkStompDeath(playerGo);
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

            if (debugShellSpawn)
            {
                debugLastShellSpawnWorld = shell.transform.position;
                debugHasLastShellSpawn = true;
                float shellMinY = float.MaxValue;
                foreach (var c in shell.GetComponentsInChildren<Collider2D>(true))
                {
                    if (c.enabled) shellMinY = Mathf.Min(shellMinY, c.bounds.min.y);
                }
                Debug.Log(
                    $"[GreenTurtle][ShellSpawn] shell.worldPos={shell.transform.position} shellMinY≈{(shellMinY < float.MaxValue ? shellMinY.ToString("F3") : "?")} " +
                    $"footY(target)={footY:F3} turtle.pos={transform.position} dir={direction} slideVx={direction * slideSpeed:F2} parent={(parent != null ? parent.name : "null")}",
                    this);
            }
        }

        SetState(State.Sliding);
        if (playerRb != null)
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
    }

    /// <summary>플레이어 콜라이더가 이 몬스터와 충돌한 경우(루트에 Player 태그가 있을 때).</summary>
    bool IsPlayerCollision(Collision2D col)
    {
        if (col.collider != null && col.collider.CompareTag(playerTag))
            return true;
        if (col.rigidbody != null && col.rigidbody.gameObject.CompareTag(playerTag))
            return true;
        if (col.collider != null)
        {
            var rb = col.collider.GetComponentInParent<Rigidbody2D>();
            if (rb != null && rb.gameObject.CompareTag(playerTag))
                return true;
        }
        return false;
    }

    /// <summary>발/콜라이더 기준 위에서 밟았는지. Enter에서 contact가 비는 경우가 있어 bounds를 우선 사용.</summary>
    bool IsPlayerStompFromAbove(Collision2D col, GameObject playerGo)
    {
        Collider2D playerCol = playerGo.GetComponent<Collider2D>()
            ?? playerGo.GetComponentInChildren<Collider2D>(true);
        if (bodyCollider != null && playerCol != null)
        {
            if (playerCol.bounds.min.y >= bodyCollider.bounds.max.y - 0.08f)
                return true;
        }

        if (playerGo.transform.position.y <= transform.position.y + stompThreshold)
            return false;

        int n = col.contactCount;
        for (int i = 0; i < n; i++)
        {
            var c = col.GetContact(i);
            float nx = c.normal.x;
            float ny = c.normal.y;
            if (Mathf.Abs(nx) < Mathf.Abs(ny) && Mathf.Abs(ny) > 0.2f)
                return true;
        }

        if (col.relativeVelocity.y < -0.35f)
            return true;

        return false;
    }

    public void OnShellKill()
    {
        if (isDyingFromInnerWalkStomp) return;
        BeginInnerWalkStompDeath(null);
    }

    void BeginInnerWalkStompDeath(GameObject playerGo)
    {
        if (isDyingFromInnerWalkStomp) return;
        isDyingFromInnerWalkStomp = true;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.flipY = true;

        if (animator != null)
            animator.enabled = false;

        rb.gravityScale = innerWalkDeathGravityScale * MonsterDeathCollisionUtil.DeathFallGravityMultiplier;
        rb.angularVelocity = 0f;
        rb.linearVelocity = new Vector2(0f, innerWalkDeathPop);

        Collider2D[] myCols = GetComponentsInChildren<Collider2D>(true);
        MonsterDeathCollisionUtil.IgnorePlayerAndGround(myCols, playerTag, groundTag, playerGo);

        if (innerWalkDeathDestroyRoutine != null)
            StopCoroutine(innerWalkDeathDestroyRoutine);
        innerWalkDeathDestroyRoutine = StartCoroutine(InnerWalkStompDeathDestroyRoutine());
    }

    IEnumerator InnerWalkStompDeathDestroyRoutine()
    {
        float t = Mathf.Max(0.01f, innerWalkDeathDestroyDelay);
        yield return new WaitForSeconds(t);
        Destroy(gameObject);
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

    /// <summary>타일맵·자식 콜라이더는 루트에만 Ground 태그가 있는 경우가 많음.</summary>
    bool IsGroundTaggedCollider(Collider2D c)
    {
        if (c == null) return false;
        for (Transform t = c.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(groundTag))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 벽 Cast에서 앞을 막는 다른 몬스터인지. Enemy 태그 계층 또는 껍데기(쿨파/FindEnemyRoot와 동일 취지).
    /// 이게 먼저 맞으면 그 뒤 Ground 벽은 보지 않음(Enemy 레이어 제외 Cast가 몬스터를 뚫고 벽만 맞는 문제 방지).
    /// </summary>
    bool IsOtherMonsterObstacle(Collider2D c)
    {
        if (c == null || c.attachedRigidbody == rb) return false;
        if (c.GetComponentInParent<GreenTurtleShell>(true) != null) return true;
        for (Transform t = c.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(enemyTag))
                return true;
        }
        return false;
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
            // Enemy 포함: 앞 몬스터를 먼저 맞추고, 그 뒤 벽은 보지 않음(TryBumpEnemy와 상쇄 방지).
            layerMask = Physics2D.DefaultRaycastLayers
        };

        int n = bodyCollider.Cast(moveDir, filter, wallCastBuffer, wallCheckCastDistance);
        for (int i = 0; i < n; i++)
        {
            var h = wallCastBuffer[i];
            if (h.collider == null) continue;
            if (IsOtherMonsterObstacle(h.collider))
                return;
            if (!IsGroundTaggedCollider(h.collider)) continue;
            // 수직 벽(또는 주로 옆면): 바닥만 맞으면 법선이 위쪽이라 스킵
            if (Mathf.Abs(h.normal.x) <= Mathf.Abs(h.normal.y)) continue;

            float dirBefore = direction;
            direction = -direction;
            ApplyFacingFromDirection();
            lastSideWallFlipTime = Time.time;
            if (debugLogDirectionChanges)
                DebugLogGreenTurtleFlip_WallCast(dirBefore, direction, h);
            return;
        }
    }

    /// <summary>지면 태그 충돌 중 벽(법선이 주로 좌우)이면 방향 반전. Sliding에서는 호출하지 않음.</summary>
    /// <remarks>
    /// relativeVelocity는 접촉 시 0에 가깝게 나오는 경우가 있어 rb.linearVelocity로 접근 여부를 본다.
    /// 그래도 실패하면 수평 법선만으로 1회 반전(쿨다운으로 Stay 스팸 방지).
    /// </remarks>
    void TryFlipOnSideWall(Collision2D col)
    {
        if (Time.time - lastSideWallFlipTime < sideWallFlipCooldown)
            return;
        if (!IsGroundTaggedCollider(col.collider))
            return;

        foreach (var contact in col.contacts)
        {
            Vector2 n = contact.normal;
            if (Mathf.Abs(n.x) <= Mathf.Abs(n.y))
                continue;

            float approach = Vector2.Dot(rb.linearVelocity, n);
            if (approach < -0.02f)
            {
                float dirBefore = direction;
                direction = -direction;
                ApplyFacingFromDirection();
                lastSideWallFlipTime = Time.time;
                if (debugLogDirectionChanges)
                    DebugLogGreenTurtleFlip_SideWall(dirBefore, direction, col, "approachVel");
                return;
            }
        }

        foreach (var contact in col.contacts)
        {
            Vector2 n = contact.normal;
            if (Mathf.Abs(n.x) <= Mathf.Abs(n.y))
                continue;
            float dirBefore = direction;
            direction = -direction;
            ApplyFacingFromDirection();
            lastSideWallFlipTime = Time.time;
            if (debugLogDirectionChanges)
                DebugLogGreenTurtleFlip_SideWall(dirBefore, direction, col, "horizontalNormal");
            return;
        }
    }

    /// <summary>이동 방향(반전 전) 기준: 상대 루트 X가 앞/뒤(등) 어느 쪽인지 대략 분류. 겹침이면 앞뒤 불명.</summary>
    static string ClassifyOtherAlongMoveX(float moveDirBefore, float otherX, float selfX)
    {
        const float eps = 0.03f;
        float dx = otherX - selfX;
        float frontAlong = dx * moveDirBefore;
        if (frontAlong > eps) return "앞(이동방향 쪽)";
        if (frontAlong < -eps) return "뒤(등 쪽)";
        return "X거의동일(겹침·측면)";
    }

    void DebugLogGreenTurtleFlip_EnemyBump(string callbackPhase, float dirBefore, float dirAfter, Collision2D col, GameObject otherRoot)
    {
        float ox = otherRoot != null ? otherRoot.transform.position.x : 0f;
        string side = ClassifyOtherAlongMoveX(dirBefore, ox, transform.position.x);
        string nStr = "";
        if (col != null && col.contactCount > 0)
        {
            var p = col.GetContact(0);
            nStr = $" contact[0].normal=({p.normal.x:F2},{p.normal.y:F2})";
        }
        Debug.Log(
            $"[GreenTurtle][Dir] EnemyBump | 콜백={callbackPhase} | state={currentState} | dir {dirBefore:F0}→{dirAfter:F0} | " +
            $"상대위치(이동기준)={side} | dx(other-self)={(ox - transform.position.x):F3}{nStr} | other={otherRoot?.name}",
            this);
    }

    void DebugLogGreenTurtleFlip_WallCast(float dirBefore, float dirAfter, RaycastHit2D h)
    {
        Debug.Log(
            $"[GreenTurtle][Dir] WallCast | state={currentState} | dir {dirBefore:F0}→{dirAfter:F0} | hit={h.collider?.name} " +
            $"normal=({h.normal.x:F2},{h.normal.y:F2}) dist={h.distance:F3}",
            this);
    }

    void DebugLogGreenTurtleFlip_SideWall(float dirBefore, float dirAfter, Collision2D col, string subReason)
    {
        string nStr = "";
        if (col != null && col.contactCount > 0)
        {
            var p = col.GetContact(0);
            nStr = $" contact[0].normal=({p.normal.x:F2},{p.normal.y:F2})";
        }
        Debug.Log(
            $"[GreenTurtle][Dir] GroundSideWall({subReason}) | state={currentState} | dir {dirBefore:F0}→{dirAfter:F0} | col={col.collider?.name}{nStr}",
            this);
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

    void OnDrawGizmos()
    {
        if (!debugShellSpawn || !debugHasLastShellSpawn)
            return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
        Gizmos.DrawWireSphere(debugLastShellSpawnWorld, 0.14f);
        Gizmos.DrawLine(debugLastShellSpawnWorld, debugLastShellSpawnWorld + Vector3.up * 0.35f);
    }
}
