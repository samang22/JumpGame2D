using System.Collections;
using UnityEngine;

/// <summary>
/// ??: ?? ?? ?? ??, ??(?? ?? 0.7? ??), ?? ??? 3? ? ?? ??? 3? ??.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Koopa : MonoBehaviour, IShellKillable
{
    public enum AnimStateValue
    {
        Patrol = 0,
        Jump = 1,
        BreathForward = 2,
        BreathUp = 3
    }

    public enum Phase
    {
        Patrol,
        Jumping,
        Breath
    }

    [Header("Patrol")]
    [SerializeField] private float patrolHalfWidth = 1.4f;
    [SerializeField] private float walkSpeed = 1.4f;

    [Header("Jump (??? ?? ?? + ?? ??)")]
    [SerializeField] private float jumpHorizontalSpeed = 4f;
    [SerializeField] private float jumpMinFlightTime = 0.35f;
    [SerializeField] private float jumpMaxFlightTime = 1.05f;
    [SerializeField] private float jumpMaxInitialVy = 22f;
    [Tooltip("??? ??? jumpVelocityScale(?? 0.7)")]
    [SerializeField] private float jumpVelocityScale = 0.7f;
    [Tooltip("?? ?? ?? ?? ??(?? 0.35 ? ?? ?? ??)")]
    [SerializeField] private float jumpSpeedVsBoongBoong = 0.35f;
    [Tooltip("? ??? ?? ? ?? ???? ??(?)")]
    [SerializeField] private float timeBetweenCycles = 2.2f;

    [Header("Breath")]
    [SerializeField] private GameObject breathFrontPrefab;
    [SerializeField] private GameObject breathUpPrefab;
    [SerializeField] private Transform breathSpawnPoint;
    [SerializeField] private Vector2 breathSpawnOffset = new Vector2(0.55f, 0.35f);
    [SerializeField] private int breathCountPerPhase = 6;
    [Tooltip("?? ??? ?? ??(???? ??)")]
    [SerializeField] private float breathForwardSpeed = 3.2f;
    [Tooltip("?? ? ? ?? ??(? ?~??? ? ??)")]
    [SerializeField] private float breathForwardHeightSpread = 0.4f;
    [Tooltip("? ??? ?? Y ??(????). ?? ??? ??? ??? ??")]
    [SerializeField] private float breathForwardSpawnHeightJitter = 0.12f;
    [Tooltip("??? ??? ?? ??? ?? ?? ???? ??")]
    [SerializeField] private float breathForwardVelYMin = -0.12f;
    [SerializeField] private float breathForwardVelYMax = 0.12f;
    [SerializeField] private float breathInterval = 0.45f;
    [SerializeField] private float breathWindup = 0.08f;
    [Tooltip("? ???: ??? ???? ? ?? Y ??")]
    [SerializeField] private float skyBreathTopYOffset = 0.25f;
    [Tooltip("? ??? ?? ??? ?? ??(???? ??)")]
    [SerializeField] private float breathUpSpawnHeightSpread = 0.4f;
    [Tooltip("? ??? ?? Y ??(????)")]
    [SerializeField] private float breathUpSpawnHeightJitter = 0.12f;
    [Tooltip("? ??? ?? ???(?? ??? Camera.main)")]
    [SerializeField] private Camera breathCamera;

    [Header("Animator (??)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string paramState = "State";

    [Header("Player / Stomp")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private float stompThreshold = 0.2f;
    [SerializeField] private float stompBounce = 8f;

    [Header("Death")]
    [SerializeField] private float deathPopVelocity = 4f;
    [SerializeField] private float deathMinLifetimeBeforeDestroy = 3f;
    [SerializeField] private float destroyAfterDeathDelay = 0.45f;

    [Header("Kinematic ??")]
    [SerializeField] private float kinematicFallAcceleration = 28f;
    [SerializeField] private float kinematicMaxFallSpeed = 13f;
    [SerializeField] private float kinematicGroundCheckDistance = 0.15f;

    [Header("Facing")]
    [SerializeField] private bool spriteFacesLeftByDefault = true;

    private Phase phase = Phase.Patrol;
    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private float initialGravityScale;
    private Vector2 spawnAnchor;
    private float patrolDirection = 1f;
    private Transform playerTransform;
    private float nextJumpTime;
    private float jumpStartedTime = -999f;
    private Collider2D[] myColliders;
    private Coroutine breathRoutine;
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
        nextJumpTime = Time.time + timeBetweenCycles;
        ApplyAnimatorState(AnimStateValue.Patrol);
    }

    private void OnDisable()
    {
        if (breathRoutine != null)
        {
            StopCoroutine(breathRoutine);
            breathRoutine = null;
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
                        phase = Phase.Breath;
                        rb.linearVelocity = Vector2.zero;
                        if (breathRoutine != null)
                            StopCoroutine(breathRoutine);
                        breathRoutine = StartCoroutine(BreathAttackRoutine());
                    }
                    break;
                }
            case Phase.Breath:
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

        float s = Mathf.Max(0.01f, jumpVelocityScale) * Mathf.Max(0.01f, jumpSpeedVsBoongBoong);
        rb.linearVelocity = new Vector2(vx0 * s, vy0 * s);
        phase = Phase.Jumping;
        jumpStartedTime = Time.time;
        ApplyAnimatorState(AnimStateValue.Jump);
        nextJumpTime = float.MaxValue;
    }

    private IEnumerator BreathAttackRoutine()
    {
        if (breathFrontPrefab == null && breathUpPrefab == null)
        {
            phase = Phase.Patrol;
            ApplyAnimatorState(AnimStateValue.Patrol);
            nextJumpTime = Time.time + timeBetweenCycles;
            breathRoutine = null;
            yield break;
        }

        ApplyAnimatorState(AnimStateValue.BreathForward);
        for (int i = 0; i < breathCountPerPhase; i++)
        {
            if (isDead) yield break;
            while (GameState.IsMapEditMode && !isDead)
                yield return null;
            if (breathWindup > 0f)
                yield return new WaitForSeconds(breathWindup);
            SpawnBreathForward(i);
            if (i < breathCountPerPhase - 1 && breathInterval > 0f)
                yield return new WaitForSeconds(breathInterval);
        }

        if (breathInterval > 0f)
            yield return new WaitForSeconds(breathInterval);

        ApplyAnimatorState(AnimStateValue.BreathUp);
        for (int i = 0; i < breathCountPerPhase; i++)
        {
            if (isDead) yield break;
            while (GameState.IsMapEditMode && !isDead)
                yield return null;
            if (i == 0 && breathWindup > 0f)
                yield return new WaitForSeconds(breathWindup);
            SpawnBreathUp(i);
        }

        if (isDead) yield break;

        phase = Phase.Patrol;
        ApplyAnimatorState(AnimStateValue.Patrol);
        nextJumpTime = Time.time + timeBetweenCycles;
        breathRoutine = null;
    }

    private float GetFacingSignX()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            if (spriteFacesLeftByDefault)
                return sr.flipX ? 1f : -1f;
            return sr.flipX ? -1f : 1f;
        }
        return patrolDirection;
    }

    private Vector3 GetBreathSpawnPosition(float signX)
    {
        if (breathSpawnPoint != null)
            return breathSpawnPoint.position;
        var p = transform.position;
        p.x += signX * Mathf.Abs(breathSpawnOffset.x);
        p.y += breathSpawnOffset.y;
        return p;
    }

    private void SpawnBreathForward(int shotIndex)
    {
        if (GameState.IsMapEditMode || breathFrontPrefab == null) return;
        float sx = GetFacingSignX();
        Vector3 pos = GetBreathSpawnPosition(sx);
        int n = Mathf.Max(1, breathCountPerPhase);
        if (n > 1)
            pos.y += Mathf.Lerp(-breathForwardHeightSpread, breathForwardHeightSpread, (float)shotIndex / (n - 1));
        if (breathForwardSpawnHeightJitter > 0f)
            pos.y += Random.Range(-breathForwardSpawnHeightJitter, breathForwardSpawnHeightJitter);
        float vy = n > 1
            ? Mathf.Lerp(breathForwardVelYMin, breathForwardVelYMax, (float)shotIndex / (n - 1))
            : (breathForwardVelYMin + breathForwardVelYMax) * 0.5f;
        var go = Instantiate(breathFrontPrefab, pos, Quaternion.identity);
        if (go.TryGetComponent<KoopaFrontBreath>(out var proj))
            proj.LaunchWithVelocity(new Vector2(sx * breathForwardSpeed, vy), myColliders);
    }

    private void SpawnBreathUp(int slotIndex)
    {
        if (GameState.IsMapEditMode || breathUpPrefab == null) return;
        Vector3 pos = GetSkyRainSpawnPosition(slotIndex);
        var go = Instantiate(breathUpPrefab, pos, Quaternion.identity);
        if (go.TryGetComponent<KoopaUpBreath>(out var up))
            up.LaunchRainFromSky(myColliders);
    }

    Vector3 GetSkyRainSpawnPosition(int index)
    {
        int total = Mathf.Max(1, breathCountPerPhase);
        var cam = breathCamera != null ? breathCamera : Camera.main;
        float left;
        float right;
        float topY;
        float z = transform.position.z;
        if (cam != null && cam.orthographic)
        {
            float halfW = cam.orthographicSize * cam.aspect;
            left = cam.transform.position.x - halfW;
            right = cam.transform.position.x + halfW;
            topY = cam.transform.position.y + cam.orthographicSize + skyBreathTopYOffset;
        }
        else if (cam != null)
        {
            var topLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, Mathf.Abs(cam.transform.position.z - z)));
            var topRight = cam.ViewportToWorldPoint(new Vector3(1f, 1f, Mathf.Abs(cam.transform.position.z - z)));
            left = topLeft.x;
            right = topRight.x;
            topY = topLeft.y + skyBreathTopYOffset;
        }
        else
        {
            left = transform.position.x - 9f;
            right = transform.position.x + 9f;
            topY = transform.position.y + 11f + skyBreathTopYOffset;
        }

        float t = total <= 1 ? 0.5f : (index + 0.5f) / total;
        float x = Mathf.Lerp(left, right, t);
        float y = topY;
        if (total > 1)
            y += Mathf.Lerp(-breathUpSpawnHeightSpread, breathUpSpawnHeightSpread, (float)index / (total - 1));
        if (breathUpSpawnHeightJitter > 0f)
            y += Random.Range(-breathUpSpawnHeightJitter, breathUpSpawnHeightJitter);
        return new Vector3(x, y, z);
    }

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
            if (playerRb != null)
                playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
            BeginDeath();
            return;
        }

        playerController?.TakeDamage();
    }

    public void OnShellKill()
    {
        BeginDeath();
    }

    private void BeginDeath()
    {
        if (isDead) return;
        isDead = true;

        if (breathRoutine != null)
        {
            StopCoroutine(breathRoutine);
            breathRoutine = null;
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
