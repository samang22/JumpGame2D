using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Flagpole goal sequence:
/// 1. Player touches pole → grab animation + snap to pole X
/// 2. Player slides down + Koopa flag down / Mario flag up simultaneously
/// 3. Player reaches bottom → victory animation
/// 4. OnGoalReached fires → PlaySceneController shows clear panel
/// </summary>
public class GoalMarker : MonoBehaviour
{
    [Header("Pole Structure")]
    [Tooltip("폴대 꼭대기 위치 (빈 GameObject)")]
    [SerializeField] private Transform poleTop;
    [Tooltip("폴대 바닥 위치 (빈 GameObject)")]
    [SerializeField] private Transform poleBottom;
    [Tooltip("쿠파 깃발 Transform (시작: 꼭대기 → 바닥으로 이동)")]
    [SerializeField] private Transform koopaFlag;
    [Tooltip("마리오 깃발 Transform (시작: 바닥 → 꼭대기로 이동)")]
    [SerializeField] private Transform marioFlag;

    [Header("Sequence Settings")]
    [SerializeField] private float slideSpeed = 4f;
    [SerializeField] private float flagSpeed = 3f;
    [SerializeField] private float victoryDuration = 1f;
    [SerializeField] private string playerTag = "Player";

    public event Action OnGoalReached;

    private bool reached;

    private void Start()
    {
        if (marioFlag != null)
            marioFlag.gameObject.SetActive(false);
    }

    public void ResetGoal()
    {
        reached = false;

        if (koopaFlag != null && poleTop != null)
        {
            var p = koopaFlag.position;
            p.y = poleTop.position.y;
            koopaFlag.position = p;
        }
        if (marioFlag != null && poleBottom != null)
        {
            var p = marioFlag.position;
            p.y = poleBottom.position.y;
            marioFlag.position = p;
            marioFlag.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (reached) return;
        if (!other.CompareTag(playerTag)) return;

        reached = true;
        StartCoroutine(GoalSequence(other.gameObject));
    }

    private IEnumerator GoalSequence(GameObject playerObj)
    {
        var playerController = playerObj.GetComponent<PlayerController>();
        var rb = playerObj.GetComponent<Rigidbody2D>();

        // 1. 타이머 정지
        var gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null) gameTimer.StopTimer();

        // 마리오 깃발 표시
        if (marioFlag != null)
            marioFlag.gameObject.SetActive(true);

        // 폴대 X에 스냅, Y는 폴대 범위 안으로 클램프
        float poleX = transform.position.x;
        float topY = poleTop != null ? poleTop.position.y : playerObj.transform.position.y;
        float bottomY = poleBottom != null ? poleBottom.position.y : topY - 5f;
        float startY = Mathf.Clamp(playerObj.transform.position.y, bottomY, topY);

        playerObj.transform.position = new Vector3(poleX, startY, playerObj.transform.position.z);

        // 2. Grab 애니메이션 + 물리 차단
        if (playerController != null)
            playerController.EnterGrabPole();

        // 3. 미끄러짐 + 깃발 동시 이동
        while (playerObj.transform.position.y > bottomY + 0.01f)
        {
            float dt = Time.deltaTime;

            // 플레이어 슬라이드
            float newY = Mathf.MoveTowards(playerObj.transform.position.y, bottomY, slideSpeed * dt);
            playerObj.transform.position = new Vector3(poleX, newY, playerObj.transform.position.z);

            // 쿠파 깃발 ↓
            if (koopaFlag != null)
            {
                var kp = koopaFlag.position;
                kp.y = Mathf.MoveTowards(kp.y, bottomY, flagSpeed * dt);
                koopaFlag.position = kp;
            }

            // 마리오 깃발 ↑
            if (marioFlag != null)
            {
                var mp = marioFlag.position;
                mp.y = Mathf.MoveTowards(mp.y, topY, flagSpeed * dt);
                marioFlag.position = mp;
            }

            yield return null;
        }

        // 4. 깃발이 목표 위치까지 완전히 이동할 때까지 대기
        bool koopaReached = koopaFlag == null;
        bool marioReached = marioFlag == null;

        while (!koopaReached || !marioReached)
        {
            float dt = Time.deltaTime;

            if (!koopaReached && koopaFlag != null)
            {
                var kp = koopaFlag.position;
                kp.y = Mathf.MoveTowards(kp.y, bottomY, flagSpeed * dt);
                koopaFlag.position = kp;
                if (Mathf.Abs(kp.y - bottomY) < 0.01f) koopaReached = true;
            }

            if (!marioReached && marioFlag != null)
            {
                var mp = marioFlag.position;
                mp.y = Mathf.MoveTowards(mp.y, topY, flagSpeed * dt);
                marioFlag.position = mp;
                if (Mathf.Abs(mp.y - topY) < 0.01f) marioReached = true;
            }

            yield return null;
        }

        // 5. Victory 애니메이션
        if (playerController != null)
            playerController.EnterVictory();

        yield return new WaitForSeconds(victoryDuration);

        // 5. TestPlay면 Edit 모드로 복귀, 아니면 클리어 이벤트 발생
        if (GameState.IsTestPlay)
        {
            var editController = FindFirstObjectByType<EditSceneController>();
            if (editController != null)
                editController.OnBackToEditClicked();
        }
        else
        {
            OnGoalReached?.Invoke();
        }
    }
}
