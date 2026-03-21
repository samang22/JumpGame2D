using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState { Small, Big, Flower }
public enum PowerUpType { Mushroom, Flower }

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 80f;
    [SerializeField] private float skidDeceleration = 15f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float deathJumpForce = 8f;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private float deathYThreshold = -5f;
    [Tooltip("이 속도 이상이면 Run 애니메이션 재생 (0~1 비율, maxSpeed 기준)")]
    [SerializeField] private float runThreshold = 0.8f;

    [Header("Animator Override Controllers")]
    [SerializeField] private AnimatorOverrideController smallOverride;
    [SerializeField] private AnimatorOverrideController bigOverride;
    [SerializeField] private AnimatorOverrideController flowerOverride;

    [Header("Invincibility")]
    [SerializeField] private float invincibleDuration = 2f;

    [Header("Power-up transform (마리오식 깜빡임 후 변신)")]
    [Tooltip("아이템 획득 후 이 시간이 지나면 Animator/상태가 실제로 바뀜.")]
    [SerializeField] private float powerUpApplyDelay = 0.45f;
    [Tooltip("깜빡임 간격(초).")]
    [SerializeField] private float powerUpFlashInterval = 0.08f;
    [Tooltip("변신 적용 후 추가로 깜빡일 총 시간.")]
    [SerializeField] private float powerUpFlashAfterTransform = 0.35f;
    [Tooltip("파워업 연출 중 이동·점프 입력 무시.")]
    [SerializeField] private bool freezeInputDuringPowerUp = true;

    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private PlayerInputActions inputActions;

    private float moveInput;
    private bool isGrounded;
    private bool isDead;
    private bool isInGoalSequence;
    private bool isInvincible;
    private bool isPowerUpTransition;

    private Vector3 spawnPosition;
    private PlayerState playerState = PlayerState.Small;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>(true);
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        inputActions = new PlayerInputActions();
    }

    void Start()
    {
        spawnPosition = transform.position;
        // 물리/충돌이 Start보다 먼저 돌 수 있음 — 이미 PowerUp으로 playerState가 바뀐 경우 Small로 덮어쓰지 않음
        SetState(playerState);
    }

    public void SetSpawnPosition(Vector3 pos) => spawnPosition = pos;

    void OnEnable()
    {
        if (inputActions == null) inputActions = new PlayerInputActions();
        inputActions.Player.Enable();
        inputActions.Player.Jump.performed += OnJump;
    }

    void OnDisable()
    {
        if (inputActions == null) return;
        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Disable();
    }

    void Update()
    {
        if (isDead || isInGoalSequence) return;

        if (transform.position.y < deathYThreshold)
            OnDead();

        if (inputActions == null) return;
        if (freezeInputDuringPowerUp && isPowerUpTransition)
        {
            moveInput = 0f;
            return;
        }

        moveInput = inputActions.Player.Move.ReadValue<float>();

        if (moveInput > 0) spriteRenderer.flipX = false;
        else if (moveInput < 0) spriteRenderer.flipX = true;

        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("speed", currentSpeed);
        animator.SetBool("isGrounded", isGrounded);

        // Run: 최대 속도의 runThreshold 이상
        bool isRunning = currentSpeed >= maxSpeed * runThreshold;
        animator.SetBool("isRunning", isRunning);

        // Skid: Run 상태에서만 반대 방향 입력 시 발동
        bool isSkidding = isRunning
                       && ((rb.linearVelocity.x > 0.1f && moveInput < 0f)
                        || (rb.linearVelocity.x < -0.1f && moveInput > 0f));
        animator.SetBool("isSkidding", isSkidding);
    }

    void FixedUpdate()
    {
        if (isDead || isInGoalSequence) return;

        if (freezeInputDuringPowerUp && isPowerUpTransition)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float currentVelX = rb.linearVelocity.x;
        bool isRunning = Mathf.Abs(currentVelX) >= maxSpeed * runThreshold;
        bool isSkidding = isRunning
                       && ((currentVelX > 0.1f && moveInput < 0f)
                        || (currentVelX < -0.1f && moveInput > 0f));

        if (isSkidding)
        {
            currentVelX = Mathf.MoveTowards(currentVelX, 0f, skidDeceleration * Time.fixedDeltaTime);
        }
        else if (moveInput != 0)
        {
            currentVelX = Mathf.MoveTowards(currentVelX, moveInput * maxSpeed, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentVelX = Mathf.MoveTowards(currentVelX, 0f, deceleration * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector2(currentVelX, rb.linearVelocity.y);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (isDead || isInGoalSequence) return;
        if (freezeInputDuringPowerUp && isPowerUpTransition) return;
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (TryPickupPowerUp(collision.collider)) return;
        if (collision.gameObject.CompareTag("Ground")) isGrounded = true;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGrounded = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGrounded = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (TryPickupPowerUp(other)) return;
        if (other.CompareTag("Hazard")) TakeDamage();
    }

    bool TryPickupPowerUp(Collider2D other)
    {
        if (isDead || isInGoalSequence || isPowerUpTransition) return false;
        var item = other.GetComponent<PowerUpItem>() ?? other.GetComponentInParent<PowerUpItem>();
        if (item == null) return false;
        return item.TryConsumeByPlayer(this);
    }

    // ── 데미지 ──────────────────────────────────────────────

    public void TakeDamage()
    {
        if (isInvincible || isDead || isInGoalSequence || isPowerUpTransition) return;

        switch (playerState)
        {
            case PlayerState.Flower:
                SetState(PlayerState.Big);
                StartCoroutine(InvincibleRoutine());
                break;
            case PlayerState.Big:
                SetState(PlayerState.Small);
                StartCoroutine(InvincibleRoutine());
                break;
            case PlayerState.Small:
                OnDead();
                break;
        }
    }

    private IEnumerator InvincibleRoutine()
    {
        isInvincible = true;
        float elapsed = 0f;
        while (elapsed < invincibleDuration)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        spriteRenderer.enabled = true;
        isInvincible = false;
    }

    // ── 파워업 ──────────────────────────────────────────────

    public void PowerUp(PowerUpType type)
    {
        if (isDead || isInGoalSequence || isPowerUpTransition) return;

        PlayerState target;
        switch (type)
        {
            case PowerUpType.Mushroom:
                if (playerState != PlayerState.Small) return;
                target = PlayerState.Big;
                break;
            case PowerUpType.Flower:
                if (playerState == PlayerState.Flower) return;
                target = PlayerState.Flower;
                break;
            default:
                return;
        }

        StartCoroutine(PowerUpTransformRoutine(target));
    }

    private IEnumerator PowerUpTransformRoutine(PlayerState targetState)
    {
        isPowerUpTransition = true;
        float elapsed = 0f;

        while (elapsed < powerUpApplyDelay)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(powerUpFlashInterval);
            elapsed += powerUpFlashInterval;
        }

        SetState(targetState);
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        elapsed = 0f;
        while (elapsed < powerUpFlashAfterTransform)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(powerUpFlashInterval);
            elapsed += powerUpFlashInterval;
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;
        isPowerUpTransition = false;
    }

    private void SetState(PlayerState newState)
    {
        playerState = newState;
        AnimatorOverrideController overrideController = newState switch
        {
            PlayerState.Big    => bigOverride,
            PlayerState.Flower => flowerOverride,
            _                  => smallOverride
        };
        if (animator == null) return;
        if (overrideController != null)
            animator.runtimeAnimatorController = overrideController;
        else if (newState != PlayerState.Small)
            Debug.LogWarning($"[PlayerController] {newState}용 AnimatorOverrideController 슬롯이 비어 있어 애니가 바뀌지 않습니다.");
    }

    // ── 사망 ────────────────────────────────────────────────

    private void OnDead()
    {
        if (isDead) return;
        isDead = true;

        moveInput = 0f;
        if (col != null) col.enabled = false;

        animator.SetBool("isDead", true);
        animator.SetBool("isGrounded", false);
        rb.linearVelocity = new Vector2(0f, deathJumpForce);

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        transform.position = spawnPosition;
        rb.linearVelocity = Vector2.zero;
        if (col != null) col.enabled = true;

        SetState(PlayerState.Small);
        animator.SetBool("isDead", false);
        animator.SetBool("isGrounded", false);

        isGrounded = false;
        isInvincible = false;
        isDead = false;
    }

    // ── 골 시퀀스 ───────────────────────────────────────────

    public void SetGrabbing(bool grabbing)
    {
        animator.SetBool("isGrabbing", grabbing);
    }

    public void EnterGrabPole()
    {
        isInGoalSequence = true;
        moveInput = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        if (col != null) col.enabled = false;

        animator.SetFloat("speed", 0f);
        animator.SetBool("isGrounded", false);
        animator.SetBool("isDead", false);
        animator.SetBool("victory", false);
        animator.SetBool("grabPole", true);
    }

    public void EnterVictory()
    {
        animator.SetBool("grabPole", false);
        animator.SetBool("victory", true);

        if (col != null) col.enabled = true;
        rb.gravityScale = 1f;
    }
}
