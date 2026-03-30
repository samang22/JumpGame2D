using UnityEngine;

/// <summary>
/// 쿠파 상향 브레스(낙하형). 스폰 위치는 Koopa가 화면 상단에 두고, 상승 없이 곧바로 낙하(비).
/// Animator Bool <c>isDown</c> = true 로 낙하 클립.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class KoopaUpBreath : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private float maxLifetime = 14f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [Tooltip("낙하 클립 — Animator Bool 파라미터 이름")]
    [SerializeField] private string paramIsDown = "isDown";

    [Header("Fall")]
    [SerializeField] private float fallGravityScale = 0.55f;
    [Tooltip("낙하 시 최대 아래 방향 속도")]
    [SerializeField] private float maxFallSpeed = 13f;
    [Tooltip("낙하 중 속도 방향으로 회전(끄면 스프라이트/애니 그대로)")]
    [SerializeField] private bool alignRotationWhenFalling = false;

    private Rigidbody2D rb;
    private float initialGravityScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        initialGravityScale = rb.gravityScale;
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        if (maxLifetime > 0f)
            Destroy(gameObject, maxLifetime);
    }

    /// <summary>스폰 위치는 이미 하늘(화면 위)로 잡혀 있어야 함. 상승 없이 바로 낙하.</summary>
    public void LaunchRainFromSky(Collider2D[] ownerCollidersToIgnore)
    {
        IgnoreOwnerColliders(ownerCollidersToIgnore);
        rb.gravityScale = fallGravityScale > 0f ? fallGravityScale : initialGravityScale;
        rb.linearVelocity = Vector2.zero;
        SetIsDownAnimator(true);
    }

    void SetIsDownAnimator(bool down)
    {
        if (animator == null || string.IsNullOrEmpty(paramIsDown)) return;
        animator.SetBool(paramIsDown, down);
    }

    void IgnoreOwnerColliders(Collider2D[] ownerCollidersToIgnore)
    {
        if (ownerCollidersToIgnore == null) return;
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

    private void FixedUpdate()
    {
        var v = rb.linearVelocity;
        if (v.y < -maxFallSpeed)
            v.y = -maxFallSpeed;
        rb.linearVelocity = new Vector2(0f, v.y);

        if (alignRotationWhenFalling && v.sqrMagnitude > 0.01f)
        {
            float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            rb.SetRotation(ang);
        }
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
