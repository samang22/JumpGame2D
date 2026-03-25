using UnityEngine;

/// <summary>
/// 스폰된 껍데기. Idle에서 플레이어가 옆에서 밀면 등속으로 굴러감(Move).
/// 이동 중 일반 적(EnemyHealth 없음)은 즉사, EnemyHealth 있으면 1 데미지.
/// 플레이어는 밟으면 정지, 옆·위 등 밟힘 아닌 접촉 시 데미지(이동 중일 때).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class GreenTurtleShell : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("킥된 뒤 등속 수평 속도")]
    [SerializeField] private float kickSpeed = 14f;
    [Tooltip("애니 Move 판정용 (속도 임계)")]
    [SerializeField] private float moveSpeedThreshold = 0.05f;

    [Header("Contacts")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private float stompThreshold = 0.2f;
    [Tooltip("밟았을 때 플레이어에게 줄 위쪽 속도 (GreenTurtle stompBounce와 맞춤)")]
    [SerializeField] private float stompBounce = 8f;

    [Header("Animator")]
    [Tooltip("비어 있으면 자기 또는 자식에서 Animator 검색")]
    [SerializeField] private Animator animator;
    [SerializeField] private string paramIsMove = "isMove";

    [Header("Wall bounce (GreenTurtle과 동일 개념)")]
    [Tooltip("옆벽 반전 후 연속 반전 방지(초)")]
    [SerializeField] private float sideWallFlipCooldown = 0.2f;
    [Tooltip("이동 방향으로 Cast — 모서리에서 벽 법선만으로는 반전이 안 될 때")]
    [SerializeField] private float wallCheckCastDistance = 0.12f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private bool launched;
    private float shellMoveDir = 1f;
    private float lastSideWallFlipTime = -999f;
    private readonly RaycastHit2D[] wallCastBuffer = new RaycastHit2D[8];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        ApplyShellFacing();
    }

    private void FixedUpdate()
    {
        if (GameState.IsMapEditMode)
        {
            rb.linearVelocity = Vector2.zero;
            launched = false;
            UpdateAnimatorMoveBool();
            return;
        }

        if (GameState.IsVictory)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateAnimatorMoveBool();
            return;
        }

        if (launched)
        {
            TryFlipIfWallAhead();
            rb.linearVelocity = new Vector2(shellMoveDir * kickSpeed, rb.linearVelocity.y);
        }

        UpdateAnimatorMoveBool();
    }

    private void UpdateAnimatorMoveBool()
    {
        if (animator == null || string.IsNullOrEmpty(paramIsMove)) return;
        bool moving = launched && Mathf.Abs(rb.linearVelocity.x) > moveSpeedThreshold;
        animator.SetBool(paramIsMove, moving);
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (GameState.IsMapEditMode) return;
        if (GameState.IsVictory) return;

        if (col.gameObject.CompareTag(groundTag) && launched)
            TryFlipOnSideWall(col);

        GameObject playerGo = FindTaggedParent(col.gameObject, playerTag);
        if (playerGo != null)
        {
            HandlePlayerCollision(col, playerGo);
            return;
        }

        GameObject enemyGo = FindTaggedParent(col.gameObject, enemyTag);
        if (enemyGo != null)
            HandleEnemyCollision(enemyGo);
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (GameState.IsMapEditMode) return;
        if (GameState.IsVictory) return;
        if (col.gameObject.CompareTag(groundTag) && launched)
            TryFlipOnSideWall(col);
    }

    /// <summary>모서리 등: 이동 방향으로 Cast해 수직 Ground면 shellMoveDir 반전.</summary>
    void TryFlipIfWallAhead()
    {
        if (Time.time - lastSideWallFlipTime < sideWallFlipCooldown)
            return;
        if (bodyCollider == null) return;

        Vector2 moveDir = new Vector2(shellMoveDir, 0f);
        if (Mathf.Abs(moveDir.x) < 0.01f) return;

        var filter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = Physics2D.DefaultRaycastLayers
        };

        int n = bodyCollider.Cast(moveDir, filter, wallCastBuffer, wallCheckCastDistance);
        for (int i = 0; i < n; i++)
        {
            var h = wallCastBuffer[i];
            if (h.collider == null || !h.collider.CompareTag(groundTag)) continue;
            if (Mathf.Abs(h.normal.x) <= Mathf.Abs(h.normal.y)) continue;

            shellMoveDir = -shellMoveDir;
            ApplyShellFacing();
            lastSideWallFlipTime = Time.time;
            return;
        }
    }

    /// <summary>접촉 법선·상대속도로 옆벽에 밀려 들어올 때 shellMoveDir 반전 (GreenTurtle TryFlipOnSideWall과 동일).</summary>
    void TryFlipOnSideWall(Collision2D col)
    {
        if (Time.time - lastSideWallFlipTime < sideWallFlipCooldown)
            return;

        foreach (var contact in col.contacts)
        {
            Vector2 n = contact.normal;
            if (Mathf.Abs(n.x) <= Mathf.Abs(n.y))
                continue;

            float approach = Vector2.Dot(col.relativeVelocity, n);
            if (approach >= -0.02f)
                continue;

            shellMoveDir = -shellMoveDir;
            ApplyShellFacing();
            lastSideWallFlipTime = Time.time;
            break;
        }
    }

    private void HandlePlayerCollision(Collision2D col, GameObject playerGo)
    {
        var playerRb = col.rigidbody != null
            ? col.rigidbody
            : playerGo.GetComponent<Rigidbody2D>()
              ?? playerGo.GetComponentInParent<Rigidbody2D>();
        var playerController = playerGo.GetComponent<PlayerController>()
            ?? playerGo.GetComponentInParent<PlayerController>();

        bool isStomped = IsPlayerStompFromAbove(col, playerGo);

        if (isStomped)
        {
            launched = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (playerRb != null)
                playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, stompBounce);
            return;
        }

        if (launched)
        {
            playerController?.TakeDamage();
            return;
        }

        shellMoveDir = playerGo.transform.position.x < transform.position.x ? 1f : -1f;
        launched = true;
        ApplyShellFacing();
    }

    private void HandleEnemyCollision(GameObject enemyRoot)
    {
        if (!launched) return;

        var health = enemyRoot.GetComponent<EnemyHealth>();
        if (health != null)
            health.TakeDamage(1);
        else
            Destroy(enemyRoot);
    }

    /// <summary>콜백 시점의 linearVelocity 대신 껍데기 윗면 접촉(법선 위쪽)으로 밟힘 판정.</summary>
    bool IsPlayerStompFromAbove(Collision2D col, GameObject playerGo)
    {
        if (playerGo.transform.position.y <= transform.position.y + stompThreshold)
            return false;
        foreach (var c in col.contacts)
        {
            if (c.normal.y > 0.4f)
                return true;
        }
        return false;
    }

    static GameObject FindTaggedParent(GameObject go, string tag)
    {
        if (go == null || string.IsNullOrEmpty(tag)) return null;
        for (Transform t = go.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(tag))
                return t.gameObject;
        }
        return null;
    }

    private void ApplyShellFacing()
    {
        bool faceLeft = shellMoveDir < 0f;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null)
                sr.flipX = faceLeft;
        }
    }
}
