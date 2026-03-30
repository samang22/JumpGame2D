using System.Collections;
using UnityEngine;

/// <summary>
/// 붕붕: 스폰 기준 좌우 순찰 + 플레이어 바라보기, 주기적으로 플레이어 쪽으로 점프,
/// 밟히면 그로기 → 가드 → 다시 순찰(3번째 밟기는 사망). 사망 시에도 Int State는 그로기. Animator Bool isDeath로 전환.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BoongBoong : MonoBehaviour, IShellKillable
{
    /// <summary>Animator Int <see cref="paramState"/>에 넣는 값과 동일하게 컨트롤러를 구성할 것.</summary>
    public enum AnimStateValue
    {
        Patrol = 0,
        Jump = 1,
        Groggy = 2,
        Guard = 3
    }

    public enum Phase
    {
        Patrol,
        Jumping,
        Groggy,
        Guard
    }

    [Header("Patrol (해머 터틀과 유사)")]
    [SerializeField] private float patrolHalfWidth = 1.4f;
    [SerializeField] private float walkSpeed = 1.4f;

    [Header("Jump (플레이어 위치에 착지)")]
    [SerializeField] private float jumpInterval = 2.2f;
    [Tooltip("수평 거리로 비행 시간을 잡을 때 쓰는 기준 속도(실제 vx는 목표까지 거리·시간으로 계산)")]
    [SerializeField] private float jumpHorizontalSpeed = 4f;
    [Tooltip("비행 시간 하한·상한(초). 너무 짧으면 속도 폭주 방지")]
    [SerializeField] private float jumpMinFlightTime = 0.35f;
    [SerializeField] private float jumpMaxFlightTime = 1.05f;
    [Tooltip("계산된 초기 수직속도 클램프(위로)")]
    [SerializeField] private float jumpMaxInitialVy = 22f;
    [Tooltip("점프 초기 속도 배율(낮출수록 느림; 목표 착지와는 오차 생김)")]
    [SerializeField] private float jumpVelocityScale = 0.7f;

    [Header("Groggy / Guard / Stomp")]
    [SerializeField] private float groggyDuration = 1.8f;
    [SerializeField] private float guardDuration = 1.5f;
    [SerializeField] [Min(1)] private int stompsToKill = 3;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [Tooltip("Int: 0=Patrol, 1=Jump, 2=Groggy, 3=Guard — 컨트롤러 파라미터 이름과 동일해야 함")]
    [SerializeField] private string paramState = "State";
    [Tooltip("Bool: 사망 트랜지션")]
    [SerializeField] private string paramIsDeath = "isDeath";

    [Header("Player / Stomp")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private float stompThreshold = 0.2f;
    [SerializeField] private float stompBounce = 8f;

    [Header("Death (껍질 등)")]
    [SerializeField] private float deathPopVelocity = 4f;
    [SerializeField] private float deathMinLifetimeBeforeDestroy = 3f;
    [SerializeField] private float destroyAfterDeathDelay = 0.45f;

    [Header("Kinematic 낙하")]
    [SerializeField] private float kinematicFallAcceleration = 28f;
    [SerializeField] private float kinematicMaxFallSpeed = 13f;
    [SerializeField] private float kinematicGroundCheckDistance = 0.15f;

    private Phase phase = Phase.Patrol;
    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private float initialGravityScale;
    private RigidbodyType2D initialBodyType;
    private Vector2 spawnAnchor;
    private float patrolDirection = 1f;
    private Transform playerTransform;
    private float nextJumpTime;
    private float jumpStartedTime = -999f;
    private Collider2D[] myColliders;
    private Coroutine groggyRoutine;
    private Coroutine deathRoutine;
    private bool isDead;
    private int stompHitCount;

    public Phase CurrentPhase => phase;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        initialGravityScale = rb.gravityScale;
        initialBodyType = rb.bodyType;
        MonsterKinematicSetup.ApplyGameplayKinematic(rb);
        myColliders = GetComponentsInChildren<Collider2D>(true);
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        spawnAnchor = transform.position;
    }

    private void Start()
    {
        spawnAnchor = transform.position;
        nextJumpTime = Time.time + jumpInterval;
        SetAnimatorDeathFlag(false);
        ApplyAnimatorState(AnimStateValue.Patrol);
    }

    private void OnDisable()
    {
        if (groggyRoutine != null)
        {
            StopCoroutine(groggyRoutine);
            groggyRoutine = null;
        }
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }
    }

    private void ApplyAnimatorState(AnimStateValue state)
    {
        if (animator == null || string.IsNullOrEmpty(paramState)) return;
        animator.SetInteger(paramState, (int)state);
    }

    private void SetAnimatorDeathFlag(bool death)
    {
        if (animator == null || string.IsNullOrEmpty(paramIsDeath)) return;
        animator.SetBool(paramIsDeath, death);
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (GameState.IsMapEditMode || GameState.IsVictory)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        EnsurePlayerRef();
        UpdateFacingToPlayer();

        switch (phase)
        {
            case Phase.Patrol:
                PatrolHorizontal();
                if (MonsterKinematicFall.IsGrounded(rb, bodyCollider, groundTag, kinematicGroundCheckDistance)
                    && Time.time >= nextJumpTime)
                    StartJumpTowardPlayer();
                break;

            case Phase.Jumping:
                {
                    bool grounded = MonsterKinematicFall.IsGrounded(rb, bodyCollider, groundTag, kinematicGroundCheckDistance);
                    float vy = MonsterKinematicFall.NextVerticalVelocity(
                        rb.linearVelocity.y, grounded, kinematicFallAcceleration, kinematicMaxFallSpeed, Time.fixedDeltaTime);
                    float vx = rb.linearVelocity.x;
                    rb.linearVelocity = new Vector2(vx, vy);
                    if (Time.time - jumpStartedTime > 0.12f
                        && grounded
                        && rb.linearVelocity.y <= 0.15f)
                    {
                        phase = Phase.Patrol;
                        ApplyAnimatorState(AnimStateValue.Patrol);
                        nextJumpTime = Time.time + jumpInterval;
                    }
                    break;
                }
            case Phase.Groggy:
            case Phase.Guard:
            {
                bool grounded = MonsterKinematicFall.IsGrounded(rb, bodyCollider, groundTag, kinematicGroundCheckDistance);
                float vy = MonsterKinematicFall.NextVerticalVelocity(
                    rb.linearVelocity.y, grounded, kinematicFallAcceleration, kinematicMaxFallSpeed, Time.fixedDeltaTime);
                rb.linearVelocity = new Vector2(0f, vy);
                break;
            }
        }
    }

    private void PatrolHorizontal()
    {
        float minX = spawnAnchor.x - patrolHalfWidth;
        float maxX = spawnAnchor.x + patrolHalfWidth;
        float x = rb.position.x;
        const float eps = 0.03f;
        if (x <= minX + eps && patrolDirection < 0f)
            patrolDirection = 1f;
        else if (x >= maxX - eps && patrolDirection > 0f)
            patrolDirection = -1f;

        float vx = patrolDirection * walkSpeed;
        bool grounded = MonsterKinematicFall.IsGrounded(rb, bodyCollider, groundTag, kinematicGroundCheckDistance);
        float vy = MonsterKinematicFall.NextVerticalVelocity(
            rb.linearVelocity.y, grounded, kinematicFallAcceleration, kinematicMaxFallSpeed, Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(vx, vy);
    }

    private void StartJumpTowardPlayer()
    {
        Vector2 pos = rb.position;
        Vector2 target = playerTransform != null
            ? (Vector2)playerTransform.position
            : pos + new Vector2(patrolDirection * 2f, 0f);

        float deltaX = target.x - pos.x;
        float deltaY = target.y - pos.y;
        float a = kinematicFallAcceleration;

        float absDx = Mathf.Abs(deltaX);
        float T = absDx > 0.02f
            ? absDx / Mathf.Max(0.5f, jumpHorizontalSpeed)
            : jumpMinFlightTime;
        T = Mathf.Clamp(T, jumpMinFlightTime, jumpMaxFlightTime);

        float vx0 = deltaX / T;
        float vy0 = (deltaY + 0.5f * a * T * T) / T;
        if (jumpMaxInitialVy > 0f)
            vy0 = Mathf.Min(vy0, jumpMaxInitialVy);

        float s = Mathf.Max(0.01f, jumpVelocityScale);
        rb.linearVelocity = new Vector2(vx0 * s, vy0 * s);
        phase = Phase.Jumping;
        jumpStartedTime = Time.time;
        ApplyAnimatorState(AnimStateValue.Jump);
        nextJumpTime = float.MaxValue;
    }

    /// <summary>스프라이트 원본은 왼쪽을 향함(flipX=false). 플레이어가 오른쪽이면 flipX=true로 미러해 오른쪽을 향함.</summary>
    private void UpdateFacingToPlayer()
    {
        if (playerTransform == null) return;

        bool playerOnRight = playerTransform.position.x >= transform.position.x;
        bool flipX = playerOnRight;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null)
                sr.flipX = flipX;
        }
    }

    private void EnsurePlayerRef()
    {
        if (playerTransform != null) return;
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) playerTransform = p.transform;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (isDead) return;
        if (GameState.IsMapEditMode || GameState.IsVictory) return;

        if (MonsterGreenShellContact.TryHandleMovingShellHit(col, this, OnShellKill))
            return;

        if (!col.gameObject.CompareTag(playerTag))
            return;

        var playerRb = col.rigidbody != null
            ? col.rigidbody
            : col.gameObject.GetComponent<Rigidbody2D>()
              ?? col.gameObject.GetComponentInParent<Rigidbody2D>();
        var playerController = col.gameObject.GetComponent<PlayerController>()
            ?? col.gameObject.GetComponentInParent<PlayerController>();

        GameObject playerGo = playerRb != null ? playerRb.gameObject : col.gameObject;
        bool stomp = IsPlayerStompFromAbove(col, playerGo);

        if (stomp)
        {
            if (phase == Phase.Patrol || phase == Phase.Jumping)
            {
                stompHitCount++;
                if (stompHitCount >= stompsToKill)
                {
                    if (playerRb != null)
                        playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
                    BeginDeath();
                }
                else
                    BeginGroggy(playerRb);
            }
            else
            {
                if (playerRb != null)
                    playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
            }
            return;
        }

        playerController?.TakeDamage();
    }

    private void BeginGroggy(Rigidbody2D playerRb)
    {
        phase = Phase.Groggy;
        rb.linearVelocity = Vector2.zero;
        SetAnimatorDeathFlag(false);
        ApplyAnimatorState(AnimStateValue.Groggy);

        if (playerRb != null)
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);

        if (groggyRoutine != null)
            StopCoroutine(groggyRoutine);
        groggyRoutine = StartCoroutine(GroggyThenGuardRoutine());
    }

    private IEnumerator GroggyThenGuardRoutine()
    {
        yield return new WaitForSeconds(groggyDuration);
        if (isDead || this == null) yield break;
        if (phase != Phase.Groggy) yield break;

        phase = Phase.Guard;
        ApplyAnimatorState(AnimStateValue.Guard);

        yield return new WaitForSeconds(guardDuration);
        if (isDead || this == null) yield break;

        phase = Phase.Patrol;
        ApplyAnimatorState(AnimStateValue.Patrol);
        nextJumpTime = Time.time + jumpInterval;
        groggyRoutine = null;
    }

    public void OnShellKill()
    {
        BeginDeath();
    }

    private void BeginDeath()
    {
        if (isDead) return;
        isDead = true;

        if (groggyRoutine != null)
        {
            StopCoroutine(groggyRoutine);
            groggyRoutine = null;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = initialGravityScale * MonsterDeathCollisionUtil.DeathFallGravityMultiplier;
        rb.linearVelocity = new Vector2(0f, deathPopVelocity);
        rb.angularVelocity = 0f;

        MonsterDeathCollisionUtil.IgnorePlayerAndGround(myColliders, playerTag, groundTag);

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null) sr.flipY = true;
        }

        if (animator != null)
        {
            if (!string.IsNullOrEmpty(paramState))
                ApplyAnimatorState(AnimStateValue.Groggy);
            SetAnimatorDeathFlag(true);
        }

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DestroyAfterDeathRoutine());
    }

    private IEnumerator DestroyAfterDeathRoutine()
    {
        float wait = Mathf.Max(deathMinLifetimeBeforeDestroy, destroyAfterDeathDelay);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);
        Destroy(gameObject);
    }

    private bool IsPlayerStompFromAbove(Collision2D col, GameObject playerGo)
    {
        if (playerGo.transform.position.y <= transform.position.y + stompThreshold)
            return false;

        foreach (var c in col.contacts)
        {
            float nx = c.normal.x;
            float ny = c.normal.y;
            if (Mathf.Abs(nx) < Mathf.Abs(ny) && Mathf.Abs(ny) > 0.2f)
                return true;
        }

        if (col.relativeVelocity.y < -0.35f)
            return true;

        return false;
    }
}
