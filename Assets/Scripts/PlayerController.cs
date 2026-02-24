using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;

    private Rigidbody2D rb;
    private Animator animator;
    private bool isGrounded;
    private PlayerInputActions inputActions;
    private float moveInput;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        inputActions = new PlayerInputActions();
        spriteRenderer = GetComponent<SpriteRenderer>();
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
        // Move 입력 읽기
        moveInput = inputActions.Player.Move.ReadValue<float>();

        // 좌우 반전
        if (moveInput > 0)
        {
            spriteRenderer.flipX = false;  // 오른쪽
        }
        else if (moveInput < 0)
        {
            spriteRenderer.flipX = true;   // 왼쪽
        }

        // 속도 계산
        float speed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("speed", speed);

        // 접지 상태
        animator.SetBool("isGrounded", isGrounded);
    }

    void FixedUpdate()
    {
        // 좌우 이동
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        Debug.Log($"isGrounded : {isGrounded}");
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        Debug.Log($"Jump pressed! isGrounded: {isGrounded}");

        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
}
