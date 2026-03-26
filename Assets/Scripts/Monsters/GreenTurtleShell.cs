using UnityEngine;

/// <summary>껍데기 이동 모드. 다른 스크립트는 <see cref="GreenTurtleShell.MotionState"/>로만 판별하는 것을 권장.</summary>
public enum GreenTurtleShellMotionState
{
    /// <summary>정지 또는 수평 속도 임계 이하. 적에게 데미지 없음.</summary>
    Idle,
    /// <summary>거북 밟기 직후 등 물리 속도로만 굴러감(<c>launched</c> 아님).</summary>
    StompCoast,
    /// <summary>플레이어가 옆에서 밀어 등속(킥 후 kickSpeed 유지).</summary>
    PlayerKick,
}

/// <summary>
/// 스폰된 껍데기. Idle에서 플레이어가 옆에서 밀면 등속으로 굴러감(Move).
/// 이동 중 적: EnemyHealth면 1 데미지(HP 0이면 IShellKillable 낙하 사망), 아니면 IShellKillable 또는 즉시 Destroy.
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
    [Tooltip("충돌 콜백 시점에는 rb 속도가 이미 깎여 있을 수 있어, 상대속도로 보조 판정(제곱).")]
    [SerializeField] private float enemyHitMinRelativeSpeedSq = 0.25f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private bool launched;
    private float shellMoveDir = 1f;
    private float lastSideWallFlipTime = -999f;
    private readonly RaycastHit2D[] wallCastBuffer = new RaycastHit2D[8];
    /// <summary>이번 FixedUpdate에서 물리 스텝 직전까지의 수평 속도. 충돌로 속도가 깎인 뒤 복구할 때 사용.</summary>
    private float lastCommittedHorizontalVx;

    /// <summary>현재 이동 모드. launched·수평 속도·맵 편집/승리 상태로 계산(충돌 콜백에서도 최신).</summary>
    public GreenTurtleShellMotionState MotionState
    {
        get
        {
            if (rb == null)
                return GreenTurtleShellMotionState.Idle;
            if (GameState.IsMapEditMode || GameState.IsVictory)
                return GreenTurtleShellMotionState.Idle;
            if (launched)
                return GreenTurtleShellMotionState.PlayerKick;
            if (Mathf.Abs(rb.linearVelocity.x) > moveSpeedThreshold)
                return GreenTurtleShellMotionState.StompCoast;
            return GreenTurtleShellMotionState.Idle;
        }
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
        bool moving = MotionState != GreenTurtleShellMotionState.Idle;
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

        GameObject enemyGo = MonsterEnemyContactBounce.FindEnemyRoot(col.gameObject, enemyTag);
        if (enemyGo != null)
            HandleEnemyCollision(enemyGo, col);
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

    /// <summary>
    /// 충돌 콜백은 물리 스텝 **이후**라 rb 속도가 이미 줄어든다. <see cref="MotionState"/>만 보면 Idle로 떨어져 적을 못 잡는다.
    /// </summary>
    bool ShouldApplyEnemyHit(Collision2D col)
    {
        if (launched) return true;
        if (Mathf.Abs(lastCommittedHorizontalVx) > moveSpeedThreshold) return true;
        if (col != null && col.relativeVelocity.sqrMagnitude >= enemyHitMinRelativeSpeedSq) return true;
        return false;
    }

    private void HandleEnemyCollision(GameObject enemyRoot, Collision2D col)
    {
        if (enemyRoot == gameObject) return;
        if (!ShouldApplyEnemyHit(col)) return;

        var health = enemyRoot.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.TakeDamage(99999);
            RestoreVelocityAfterEnemySquash();
            return;
        }

        if (enemyRoot.TryGetComponent<IShellKillable>(out var shellKillable))
        {
            shellKillable.OnShellKill();
            RestoreVelocityAfterEnemySquash();
            return;
        }

        Destroy(enemyRoot);
        RestoreVelocityAfterEnemySquash();
    }

    /// <summary>몬스터와 충돌해 물리가 속도를 깎아도, 껍데기는 같은 방향·의도 속도로 통과하도록 복구.</summary>
    void RestoreVelocityAfterEnemySquash()
    {
        if (launched)
        {
            rb.linearVelocity = new Vector2(shellMoveDir * kickSpeed, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(lastCommittedHorizontalVx, rb.linearVelocity.y);
    }

    /// <summary>이동 중에도 윗면·모서리에서 굼바와 동일한 밟힘 판정(수직 우세 법선 + 상대 수직 속도).</summary>
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
