using System.Collections;
using UnityEngine;

/// <summary>
/// 굼바. 그린터틀과 동일한 보행·벽 반전. 한 번 밟히면 위아래 뒤집힌 채 제자리 5초 정지 후 다시 걷기.
/// 뒤집힌 상태에서 다시 밟으면 제거(마리오식).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Goomba : MonoBehaviour, IShellKillable
{
    public enum State
    {
        Walk = 0,
        Flipped = 1
    }

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float stompBounce = 8f;

    [Header("Stomp (뒤집힘)")]
    [SerializeField] private float flippedDuration = 5f;
    [SerializeField] private float stompThreshold = 0.2f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [Tooltip("비어 있으면 무시. 사망 트랜지션용 Bool. 뒤집힘은 SpriteRenderer.flipY + Walk 클립만 사용.")]
    [SerializeField] private string paramIsDeath = "isDeath";
    [Tooltip("사망 직후 위로 주는 세로 속도(뒤집힌 채 살짝 뜸)")]
    [SerializeField] private float deathPopVelocity = 4f;
    [Tooltip("사망 후 최소 유지 시간(초). 이 안에는 Destroy 안 함(낙하 연출용)")]
    [SerializeField] private float deathMinLifetimeBeforeDestroy = 3f;
    [Tooltip("애니 등 추가 대기. 실제 대기 = Max(최소 유지 시간, 이 값)")]
    [SerializeField] private float destroyAfterDeathDelay = 0.45f;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private bool spriteFacesRightByDefault;

    [Header("Wall (GreenTurtle과 동일)")]
    [SerializeField] private float sideWallFlipCooldown = 0.2f;
    [SerializeField] private float wallCheckCastDistance = 0.12f;

    [Header("Enemy bump (GreenTurtle과 동일)")]
    [Tooltip("상대 Enemy 루트가 이동 방향 앞에 있을 때만 방향 반전. (상대X−내X)×direction > 이 값.")]
    [SerializeField] private float enemyBumpRequireFrontAlongX = 0.02f;

    private State currentState = State.Walk;
    private Rigidbody2D rb;
    private RigidbodyType2D initialBodyType;
    private float initialGravityScale;
    private float direction = -1f;
    private float lastSideWallFlipTime = -999f;
    private Collider2D bodyCollider;
    private readonly RaycastHit2D[] wallCastBuffer = new RaycastHit2D[8];
    private Coroutine flipRecoverRoutine;
    private Coroutine deathRoutine;
    private bool isDead;
    private float lastEnemyBumpTime = -999f;

    public State CurrentState => currentState;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        initialBodyType = rb.bodyType;
        initialGravityScale = rb.gravityScale;
        bodyCollider = GetComponent<Collider2D>();
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        ApplyFacingFromDirection();
    }

    private void OnDisable()
    {
        if (flipRecoverRoutine != null)
        {
            StopCoroutine(flipRecoverRoutine);
            flipRecoverRoutine = null;
        }
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (GameState.IsMapEditMode)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (GameState.IsVictory)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (currentState == State.Flipped)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        TryFlipIfWallAhead();
        rb.linearVelocity = new Vector2(direction * walkSpeed, rb.linearVelocity.y);
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (isDead) return;
        if (GameState.IsVictory) return;

        if (col.gameObject.CompareTag(groundTag) && currentState == State.Walk)
            TryFlipOnSideWall(col);

        TryBumpEnemy(col);

        if (col.gameObject.CompareTag(playerTag))
            HandlePlayerCollision(col);
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (isDead) return;
        if (GameState.IsVictory) return;
        if (col.gameObject.CompareTag(groundTag) && currentState == State.Walk)
            TryFlipOnSideWall(col);
    }

    void TryBumpEnemy(Collision2D col)
    {
        if (currentState != State.Walk) return;
        if (Time.time - lastEnemyBumpTime < MonsterEnemyContactBounce.CooldownSeconds) return;
        var root = MonsterEnemyContactBounce.FindEnemyRoot(col.gameObject, enemyTag);
        if (root == null || root == gameObject) return;
        float dx = root.transform.position.x - transform.position.x;
        if (dx * direction <= enemyBumpRequireFrontAlongX)
            return;
        lastEnemyBumpTime = Time.time;
        direction = -direction;
        ApplyFacingFromDirection();
    }

    private void HandlePlayerCollision(Collision2D col)
    {
        var playerRb = col.rigidbody != null
            ? col.rigidbody
            : col.gameObject.GetComponent<Rigidbody2D>()
              ?? col.gameObject.GetComponentInParent<Rigidbody2D>();
        var playerController = col.gameObject.GetComponent<PlayerController>()
            ?? col.gameObject.GetComponentInParent<PlayerController>();

        GameObject playerGo = playerRb != null ? playerRb.gameObject : col.gameObject;
        bool isStomped = IsPlayerStompFromAbove(col, playerGo);

        if (currentState == State.Walk)
        {
            if (isStomped)
                BeginFlipped(playerRb);
            else
                playerController?.TakeDamage();
            return;
        }

        // Flipped
        if (isStomped)
        {
            if (playerRb != null)
                playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
            BeginDeath();
        }
        // 옆/밑 접촉: 뒤집힌 굼바는 데미지 없음
    }

    public void OnShellKill()
    {
        BeginDeath();
    }

    void BeginDeath()
    {
        if (isDead) return;
        isDead = true;

        if (flipRecoverRoutine != null)
        {
            StopCoroutine(flipRecoverRoutine);
            flipRecoverRoutine = null;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = initialGravityScale * MonsterDeathCollisionUtil.DeathFallGravityMultiplier;
        rb.linearVelocity = new Vector2(0f, deathPopVelocity);
        rb.angularVelocity = 0f;

        Collider2D[] myCols = GetComponentsInChildren<Collider2D>(true);
        MonsterDeathCollisionUtil.IgnorePlayerAndGround(myCols, playerTag, groundTag);

        // 사망 시에도 뒤집힌(밟힌) 상태 유지 — 스프라이트·애니 파라미터 모두 맞춤
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null) sr.flipY = true;
        }
        if (animator != null && !string.IsNullOrEmpty(paramIsDeath))
            animator.SetBool(paramIsDeath, true);

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DestroyAfterDeathRoutine());
    }

    IEnumerator DestroyAfterDeathRoutine()
    {
        float wait = Mathf.Max(deathMinLifetimeBeforeDestroy, destroyAfterDeathDelay);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);
        Destroy(gameObject);
    }

    void BeginFlipped(Rigidbody2D playerRb)
    {
        if (currentState != State.Walk) return;

        currentState = State.Flipped;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null) sr.flipY = true;
        }

        if (playerRb != null)
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);

        if (flipRecoverRoutine != null)
            StopCoroutine(flipRecoverRoutine);
        flipRecoverRoutine = StartCoroutine(FlipRecoverRoutine());
    }

    IEnumerator FlipRecoverRoutine()
    {
        yield return new WaitForSeconds(flippedDuration);

        if (currentState != State.Flipped || this == null) yield break;

        currentState = State.Walk;
        rb.bodyType = initialBodyType;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null) sr.flipY = false;
        }

        ApplyFacingFromDirection();
        flipRecoverRoutine = null;
    }

    bool IsPlayerStompFromAbove(Collision2D col, GameObject playerGo)
    {
        if (playerGo.transform.position.y <= transform.position.y + stompThreshold)
            return false;

        foreach (var c in col.contacts)
        {
            float nx = c.normal.x;
            float ny = c.normal.y;
            // 윗면/아랫면 쪽 접촉: 법선이 가로보다 세로가 큼 (옆면은 |nx|가 더 큼)
            if (Mathf.Abs(nx) < Mathf.Abs(ny) && Mathf.Abs(ny) > 0.2f)
                return true;
        }

        // 법선이 모서리 등으로 애매할 때: 위에 있는데 상대적으로 아래로 맞닿음
        if (col.relativeVelocity.y < -0.35f)
            return true;

        return false;
    }

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
            if (Mathf.Abs(h.normal.x) <= Mathf.Abs(h.normal.y)) continue;

            direction = -direction;
            ApplyFacingFromDirection();
            lastSideWallFlipTime = Time.time;
            return;
        }
    }

    void TryFlipOnSideWall(Collision2D col)
    {
        if (Time.time - lastSideWallFlipTime < sideWallFlipCooldown)
            return;

        foreach (var contact in col.contacts)
        {
            Vector2 n = contact.normal;
            if (Mathf.Abs(n.x) <= Mathf.Abs(n.y))
                continue;

            float approach = Vector2.Dot(col.relativeVelocity, n);
            if (approach >= -0.02f)
                continue;

            direction = -direction;
            ApplyFacingFromDirection();
            lastSideWallFlipTime = Time.time;
            break;
        }
    }

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
}
