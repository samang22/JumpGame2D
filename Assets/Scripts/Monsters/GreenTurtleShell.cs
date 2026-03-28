using UnityEngine;

/// <summary>껍데기 이동 여부. 플레이어 킥 vs 밟기 관성 구분은 내부 <c>launched</c> + RB 속도로 처리.</summary>
public enum GreenTurtleShellMoveState
{
    Idle,
    Move,
}

/// <summary>
/// 스폰된 껍데기. 벽(지면)에 닿으면 방향 전환, 마리오가 옆에서 밀면 Move, 위에서 밟으면 정지(Idle).
/// 움직이는 껍데기에 맞은 몬스터 처리는 각 몬스터 스크립트 + <see cref="MonsterGreenShellContact"/>.
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
    [Tooltip("이동 방향으로 Cast — 모서리에서 법선만으로는 반전이 안 될 때")]
    [SerializeField] private float wallCheckCastDistance = 0.12f;
    [Tooltip("외부(몬스터)에서 충돌 직전 Move 여부를 물을 때 — rb는 이미 깎였을 수 있어 상대속도 보조(제곱).")]
    [SerializeField] private float contactMoveMinRelativeSpeedSq = 0.25f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    /// <summary>플레이어 옆 킥 후 등속(kickSpeed) 유지.</summary>
    private bool launched;
    private float shellMoveDir = 1f;
    private float lastSideWallFlipTime = -999f;
    private readonly RaycastHit2D[] wallCastBuffer = new RaycastHit2D[8];
    /// <summary>이번 FixedUpdate에서 물리 스텝 직전까지의 수평 속도.</summary>
    private float lastCommittedHorizontalVx;

    /// <summary>Idle ↔ Move. 킥·밟기 관성 모두 Move.</summary>
    public GreenTurtleShellMoveState MoveState => ComputeMoveState(rb != null ? rb.linearVelocity.x : 0f);

    GreenTurtleShellMoveState ComputeMoveState(float horizontalVx)
    {
        if (rb == null)
            return GreenTurtleShellMoveState.Idle;
        if (GameState.IsMapEditMode || GameState.IsVictory)
            return GreenTurtleShellMoveState.Idle;
        if (launched)
            return GreenTurtleShellMoveState.Move;
        if (Mathf.Abs(horizontalVx) > moveSpeedThreshold)
            return GreenTurtleShellMoveState.Move;
        return GreenTurtleShellMoveState.Idle;
    }

    /// <summary>충돌 콜백 시점에 “막 전까지 Move였는지” 몬스터가 물을 때 사용.</summary>
    public GreenTurtleShellMoveState GetMoveStateAtContact(Collision2D col)
    {
        GreenTurtleShellMoveState s = ComputeMoveState(lastCommittedHorizontalVx);
        if (s == GreenTurtleShellMoveState.Move)
            return GreenTurtleShellMoveState.Move;
        if (col != null && col.relativeVelocity.sqrMagnitude >= contactMoveMinRelativeSpeedSq)
            return GreenTurtleShellMoveState.Move;
        return GreenTurtleShellMoveState.Idle;
    }

    /// <summary>몬스터가 즉사 처리한 뒤 껍데기 수평 속도를 복구할 때 호출.</summary>
    public void RestoreVelocityAfterMonsterContact()
    {
        if (launched)
        {
            rb.linearVelocity = new Vector2(shellMoveDir * kickSpeed, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(lastCommittedHorizontalVx, rb.linearVelocity.y);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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

        lastCommittedHorizontalVx = rb.linearVelocity.x;
        UpdateAnimatorMoveBool();
    }

    private void UpdateAnimatorMoveBool()
    {
        if (animator == null || string.IsNullOrEmpty(paramIsMove)) return;
        bool moving = MoveState == GreenTurtleShellMoveState.Move;
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
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (GameState.IsMapEditMode) return;
        if (GameState.IsVictory) return;
        if (col.gameObject.CompareTag(groundTag) && launched)
            TryFlipOnSideWall(col);
    }

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

    bool IsPlayerStompFromAbove(Collision2D col, GameObject playerGo)
    {
        if (playerGo.transform.position.y <= transform.position.y + stompThreshold)
            return false;

        foreach (var c in col.contacts)
        {
            float nx = c.normal.x;
            float ny = c.normal.y;
            if (Mathf.Abs(nx) < Mathf.Abs(ny) && Mathf.Abs(ny) > 0.2f)
                return true;
        }

        if (col.relativeVelocity.y < -0.35f)
            return true;

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
