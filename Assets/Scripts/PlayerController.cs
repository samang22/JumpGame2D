using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float deathJumpForce = 8f;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private float deathYThreshold = -5f;

    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D col;
    private bool isGrounded;
    private PlayerInputActions inputActions;
    private float moveInput;
    private SpriteRenderer spriteRenderer;

    private Vector3 spawnPosition;
    private bool isDead;
    private bool isInGoalSequence;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        inputActions = new PlayerInputActions();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        spawnPosition = transform.position;
    }

    public void SetSpawnPosition(Vector3 pos)
    {
        spawnPosition = pos;
    }

    void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Jump.performed += OnJump;
    }

    void OnDisable()
    {
        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Disable();
    }

    void Update()
    {
        if (isDead || isInGoalSequence) return;

        if (transform.position.y < deathYThreshold)
            OnDead();

        moveInput = inputActions.Player.Move.ReadValue<float>();

        if (moveInput > 0)
            spriteRenderer.flipX = false;
        else if (moveInput < 0)
            spriteRenderer.flipX = true;

        float speed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("speed", speed);
        animator.SetBool("isGrounded", isGrounded);
    }

    void FixedUpdate()
    {
        if (isDead || isInGoalSequence) return;

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (isDead) return;

        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Hazard"))
            OnDead();
    }

    /// <summary>GoalMarker에서 호출. 폴대 잡기 애니메이션 진입.</summary>
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

    /// <summary>GoalMarker에서 호출. 바닥 도달 후 승리 애니메이션 진입.</summary>
    public void EnterVictory()
    {
        animator.SetBool("grabPole", false);
        animator.SetBool("victory", true);

        if (col != null) col.enabled = true;
        rb.gravityScale = 1f;
    }

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

        animator.SetBool("isDead", false);
        animator.SetBool("isGrounded", false);

        isGrounded = false;
        isDead = false;
    }
}
