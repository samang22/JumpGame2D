using UnityEngine;

/// <summary>
/// 해머터틀이 던지는 해머. 중력으로 포물선 이동, 플레이어/지면 접촉 시 제거.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HammerProjectile : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private float maxLifetime = 10f;
    [Tooltip("날아가는 동안 스프라이트를 속도 방향으로 회전")]
    [SerializeField] private bool alignRotationToVelocity = true;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (maxLifetime > 0f)
            Destroy(gameObject, maxLifetime);
    }

    /// <summary>
    /// 플레이어 쪽(좌 또는 우)으로 고정 초기 속도 포물선. 거리와 무관하게 같은 vx·vy 꼴.
    /// </summary>
    /// <param name="directionSign">플레이어 방향(+1 오른쪽, -1 왼쪽)</param>
    public void LaunchFixedArc(float directionSign, float speedX, float speedY, Collider2D[] ownerCollidersToIgnore)
    {
        float sx = Mathf.Sign(directionSign);
        if (sx == 0f) sx = 1f;
        rb.linearVelocity = new Vector2(sx * Mathf.Abs(speedX), speedY);

        if (ownerCollidersToIgnore != null)
        {
            var mine = GetComponents<Collider2D>();
            foreach (var my in mine)
            {
                if (my == null || !my.enabled) continue;
                foreach (var oc in ownerCollidersToIgnore)
                {
                    if (oc != null && oc.enabled)
                        Physics2D.IgnoreCollision(my, oc, true);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!alignRotationToVelocity || rb.linearVelocity.sqrMagnitude < 0.01f)
            return;
        float ang = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
        rb.SetRotation(ang);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        if (other.CompareTag(playerTag))
        {
            var pc = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            pc?.TakeDamage();
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag(groundTag))
            Destroy(gameObject);
    }
}
