using UnityEngine;

/// <summary>
/// 녹색 거북이 적.
/// Walking: 좌우 이동, 낙하 지점에서 그냥 떨어짐.
/// Shell: 밟히면 본체가 튀어오르고 껍데기만 남음.
/// ShellMoving: 껍데기를 차면 슬라이딩, 벽에서 반사.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class GreenKoopa : MonoBehaviour
{
    public enum State { Walking, Shell, ShellMoving }

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float shellSpeed = 6f;
    [SerializeField] private float stompBounce = 8f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite shellSprite;
    [SerializeField] private GameObject bodyPrefab;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float stompThreshold = 0.2f;

    private State currentState = State.Walking;
    private Rigidbody2D rb;
    private float direction = -1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        switch (currentState)
        {
            case State.Walking:
                rb.linearVelocity = new Vector2(direction * walkSpeed, rb.linearVelocity.y);
                if (spriteRenderer != null) spriteRenderer.flipX = direction > 0;
                break;
            case State.Shell:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                break;
            case State.ShellMoving:
                rb.linearVelocity = new Vector2(direction * shellSpeed, rb.linearVelocity.y);
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        // 슬라이딩 껍데기: 벽에 닿으면 반사
        if (currentState == State.ShellMoving && col.gameObject.CompareTag(groundTag))
        {
            foreach (var contact in col.contacts)
            {
                if (Mathf.Abs(contact.normal.x) > 0.5f)
                {
                    direction = -direction;
                    break;
                }
            }
        }

        // 슬라이딩 껍데기: 다른 적 처치
        if (currentState == State.ShellMoving && col.gameObject.CompareTag(enemyTag))
        {
            Destroy(col.gameObject);
        }

        // 플레이어와 충돌
        if (col.gameObject.CompareTag(playerTag))
        {
            HandlePlayerCollision(col);
        }
    }

    private void HandlePlayerCollision(Collision2D col)
    {
        var playerRb = col.gameObject.GetComponent<Rigidbody2D>();
        var playerController = col.gameObject.GetComponent<PlayerController>();

        bool isStomped = playerRb != null
            && playerRb.linearVelocity.y < 0
            && col.gameObject.transform.position.y > transform.position.y + stompThreshold;

        switch (currentState)
        {
            case State.Walking:
                if (isStomped)
                    Stomp(playerRb);
                else
                    playerController?.TakeDamage();
                break;

            case State.Shell:
                if (isStomped)
                {
                    // 멈춰있는 껍데기를 위에서 밟으면 살짝 튀어오름
                    if (playerRb != null)
                        playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
                }
                else
                    KickShell(col.gameObject);
                break;

            case State.ShellMoving:
                if (isStomped)
                {
                    // 움직이는 껍데기를 밟으면 정지
                    currentState = State.Shell;
                    if (playerRb != null)
                        playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
                }
                else
                    playerController?.TakeDamage();
                break;
        }
    }

    private void Stomp(Rigidbody2D playerRb)
    {
        // 본체 튀어오름
        if (bodyPrefab != null)
            Instantiate(bodyPrefab, transform.position, Quaternion.identity);

        // 껍데기만 남음
        currentState = State.Shell;
        if (shellSprite != null && spriteRenderer != null)
            spriteRenderer.sprite = shellSprite;

        // 플레이어 튀어오름
        if (playerRb != null)
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
    }

    private void KickShell(GameObject player)
    {
        direction = player.transform.position.x < transform.position.x ? 1f : -1f;
        currentState = State.ShellMoving;
    }
}
