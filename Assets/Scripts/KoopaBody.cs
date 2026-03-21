using UnityEngine;

/// <summary>
/// 밟혔을 때 튀어오르다가 사라지는 거북이 본체.
/// </summary>
public class KoopaBody : MonoBehaviour
{
    [SerializeField] private float popForce = 8f;
    [SerializeField] private float destroyYThreshold = -20f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        rb.linearVelocity = new Vector2(0f, popForce);
    }

    private void Update()
    {
        if (transform.position.y < destroyYThreshold)
            Destroy(gameObject);
    }
}
