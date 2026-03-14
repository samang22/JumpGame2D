using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float respawnDelay = 1f;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isGrounded;
    private PlayerInputActions inputActions;
    private float moveInput;
    private SpriteRenderer spriteRenderer;

    private Vector3 spawnPosition;
    private bool isDead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
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
        if (isDead) return;

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
        if (isDead) return;

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

    private void OnDead()
    {
        if (isDead) return;
        isDead = true;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        spriteRenderer.enabled = false;
        yield return new WaitForSeconds(respawnDelay);

        transform.position = spawnPosition;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 1f;
        spriteRenderer.enabled = true;
        isGrounded = false;
        isDead = false;
    }
}
