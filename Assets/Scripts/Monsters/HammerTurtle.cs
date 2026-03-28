using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 해머 브로스류: 스폰 지점 기준 좌우 순찰, 스프라이트는 항상 플레이어 쪽을 향함, 일정 간격으로 포물선 해머 투척.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HammerTurtle : MonoBehaviour, IShellKillable
{
    [Header("Patrol (스폰 X 기준 ±폭)")]
    [SerializeField] private float patrolHalfWidth = 1.4f;
    [SerializeField] private float walkSpeed = 1.4f;

    [Header("Hammer")]
    [SerializeField] private GameObject hammerPrefab;
    [SerializeField] private Transform hammerSpawnPoint;
    [Tooltip("터틀 중심 기준, 플레이어가 있는 쪽(좌/우)으로만 적용되는 수평 거리(절댓값). Y는 손 높이")]
    [SerializeField] private Vector2 hammerSpawnOffset = new Vector2(0.28f, 0.55f);
    [SerializeField] private float throwInterval = 2.2f;
    [Tooltip("던질 때 가로·세로 초기속도(고정). 거리와 무관하게 같은 포물선")]
    [SerializeField] private float hammerThrowVelX = 3.2f;
    [SerializeField] private float hammerThrowVelY = 4.8f;
    [SerializeField] private float throwWindup = 0.12f;

    [Header("Animator (선택)")]
    [SerializeField] private Animator animator;
    [Tooltip("Animator Bool: 투척 모션 (컨트롤러 파라미터 isThrow와 동일)")]
    [FormerlySerializedAs("throwTriggerName")]
    [SerializeField] private string paramIsThrow = "isThrow";
    [Tooltip("Animator Bool: 사망 (컨트롤러 파라미터 isDeath와 동일)")]
    [SerializeField] private string paramIsDeath = "isDeath";

    [Header("Facing")]
    [SerializeField] private bool spriteFacesRightByDefault = true;

    [Header("Player / Stomp")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private float stompThreshold = 0.2f;
    [SerializeField] private float stompBounce = 8f;

    [Header("Kinematic 낙하 (발밑에 Ground 없을 때)")]
    [SerializeField] private float kinematicFallAcceleration = 45f;
    [SerializeField] private float kinematicMaxFallSpeed = 22f;
    [SerializeField] private float kinematicGroundCheckDistance = 0.15f;

    [Header("Death (밟힘 후 연출)")]
    [Tooltip("뒤집힌 채 위로 주는 세로 속도")]
    [SerializeField] private float deathPopVelocity = 4f;
    [Tooltip("바닥 통과 낙하 후 최소 유지 시간(초)")]
    [SerializeField] private float deathMinLifetimeBeforeDestroy = 3f;
    [SerializeField] private float destroyAfterDeathDelay = 0.45f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private float initialGravityScale;
    private Vector2 spawnAnchor;
    private float patrolDirection = 1f;
    private Transform playerTransform;
    private Collider2D[] myColliders;
    private Coroutine throwRoutine;
    private Coroutine deathRoutine;
    private bool isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        initialGravityScale = rb.gravityScale;
        MonsterKinematicSetup.ApplyGameplayKinematic(rb);
        myColliders = GetComponentsInChildren<Collider2D>(true);
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        spawnAnchor = transform.position;
    }

    private void Start()
    {
        spawnAnchor = transform.position;
        if (hammerPrefab != null && throwRoutine == null)
            throwRoutine = StartCoroutine(ThrowHammerLoop());
    }

    private void OnDisable()
    {
        if (throwRoutine != null)
        {
            StopCoroutine(throwRoutine);
            throwRoutine = null;
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

        if (GameState.IsMapEditMode || GameState.IsVictory)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        PatrolHorizontal();
        UpdateFacingToPlayer();
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

    private void UpdateFacingToPlayer()
    {
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform == null) return;

        bool playerOnRight = playerTransform.position.x >= transform.position.x;
        // 기본이 오른쪽: flipX=!playerOnRight. 기본이 왼쪽이면 인스펙터에서 spriteFacesRightByDefault 끄기
        bool flipX = spriteFacesRightByDefault ? !playerOnRight : playerOnRight;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null)
                sr.flipX = flipX;
        }
    }

    private IEnumerator ThrowHammerLoop()
    {
        var wait = new WaitForSeconds(throwInterval);
        while (!isDead)
        {
            yield return wait;
            while (GameState.IsMapEditMode && !isDead)
                yield return null;
            if (isDead || hammerPrefab == null) yield break;

            if (animator != null && !string.IsNullOrEmpty(paramIsThrow))
                animator.SetBool(paramIsThrow, true);

            if (throwWindup > 0f)
                yield return new WaitForSeconds(throwWindup);

            while (GameState.IsMapEditMode && !isDead)
                yield return null;
            if (isDead || hammerPrefab == null)
            {
                if (animator != null && !string.IsNullOrEmpty(paramIsThrow))
                    animator.SetBool(paramIsThrow, false);
                yield break;
            }

            SpawnHammer();

            if (animator != null && !string.IsNullOrEmpty(paramIsThrow))
                animator.SetBool(paramIsThrow, false);
        }
    }

    private void SpawnHammer()
    {
        if (GameState.IsMapEditMode) return;

        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) playerTransform = p.transform;
        }

        float toward = 1f;
        if (playerTransform != null)
        {
            float dx = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.001f)
                toward = Mathf.Sign(dx);
        }

        Vector3 spawnPos;
        if (hammerSpawnPoint != null)
        {
            spawnPos = hammerSpawnPoint.position;
            spawnPos.x = transform.position.x + toward * Mathf.Abs(hammerSpawnOffset.x);
        }
        else
        {
            spawnPos = transform.position;
            spawnPos.x += toward * Mathf.Abs(hammerSpawnOffset.x);
            spawnPos.y += hammerSpawnOffset.y;
        }

        var go = Instantiate(hammerPrefab, spawnPos, Quaternion.identity);
        if (go.TryGetComponent<HammerProjectile>(out var hp))
            hp.LaunchFixedArc(toward, hammerThrowVelX, hammerThrowVelY, myColliders);
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (isDead) return;
        if (GameState.IsMapEditMode || GameState.IsVictory) return;

        if (MonsterGreenShellContact.TryHandleMovingShellHit(col, this, OnShellKill))
            return;

        // 몬스터·지면 등과 부딪혀도 순찰 방향은 바꾸지 않음(물리만 적용)
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
            if (playerRb != null)
                playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
            BeginDeath();
        }
        else
        {
            playerController?.TakeDamage();
        }
    }

    public void OnShellKill()
    {
        BeginDeath();
    }

    private void BeginDeath()
    {
        if (isDead) return;
        isDead = true;

        if (throwRoutine != null)
        {
            StopCoroutine(throwRoutine);
            throwRoutine = null;
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

        if (animator != null && !string.IsNullOrEmpty(paramIsDeath))
            animator.SetBool(paramIsDeath, true);

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
