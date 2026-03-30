using UnityEngine;

/// <summary>
/// 쿠파 전방 브레스. 중력 없이 등속 직선 이동. 단일 애니 상태(기본 KoopaFrontBreath).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class KoopaFrontBreath : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private float maxLifetime = 10f;
    [Tooltip("날아가는 동안 스프라이트를 속도 방향으로 회전")]
    [SerializeField] private bool alignRotationToVelocity = true;
    [Tooltip("스프라이트가 회전 0°에서 왼쪽(-X)을 향할 때 켜기. 속도 각도에 180° 보정")]
    [SerializeField] private bool spriteFacesLeftAtDefaultRotation = true;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [Tooltip("Base Layer 단일 상태 이름(애니메이션 클립과 동일하게)")]
    [SerializeField] private string animStateName = "KoopaFrontBreath";
    [SerializeField] private int animatorLayer;

    private Rigidbody2D rb;
    private Vector2 launchVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        if (maxLifetime > 0f)
            Destroy(gameObject, maxLifetime);
    }

    public void LaunchWithVelocity(Vector2 velocity, Collider2D[] ownerCollidersToIgnore)
    {
        launchVelocity = velocity;
        rb.gravityScale = 0f;
        rb.linearVelocity = velocity;
        IgnoreOwnerColliders(ownerCollidersToIgnore);
        PlayFrontAnimation();
    }

    void PlayFrontAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(animStateName)) return;
        animator.Play(animStateName, animatorLayer, 0f);
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
        if (launchVelocity.sqrMagnitude > 0.0001f)
            rb.linearVelocity = launchVelocity;

        if (!alignRotationToVelocity || launchVelocity.sqrMagnitude < 0.01f)
            return;
        float ang = Mathf.Atan2(launchVelocity.y, launchVelocity.x) * Mathf.Rad2Deg;
        if (spriteFacesLeftAtDefaultRotation)
            ang -= 180f;
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
