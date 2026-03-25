using System.Collections;
using UnityEngine;

/// <summary>
/// 파이라냐 플랜트.
/// 파이프에서 나왔다가 들어가기를 반복.
/// 플레이어가 파이프 위 근처에 있으면 나오지 않음.
/// </summary>
public class PiranhaPlant : MonoBehaviour
{
    [Header("Positions")]
    [Tooltip("식물이 완전히 나왔을 때 위치 (파이프 꼭대기)")]
    [SerializeField] private Transform pipeTop;
    [Tooltip("식물이 완전히 숨었을 때 위치 (파이프 안)")]
    [SerializeField] private Transform hidePosition;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float waitDuration = 1.5f;

    [Header("Player Detection")]
    [Tooltip("이 X 거리 이내에 플레이어가 있으면 나오지 않음")]
    [SerializeField] private float playerNearRadius = 1f;
    [SerializeField] private string playerTag = "Player";

    private Transform player;

    private void Start()
    {
        if (hidePosition != null)
            transform.position = hidePosition.position;

        StartCoroutine(PlantRoutine());
    }

    private IEnumerator PlantRoutine()
    {
        while (true)
        {
            while (GameState.IsVictory) yield return null;

            // 숨은 상태에서 대기
            yield return new WaitForSeconds(waitDuration);

            // 플레이어 근접 시 이번 사이클 건너뜀
            if (IsPlayerNear())
                continue;

            // 나오기
            yield return MoveTo(pipeTop.position);

            // 꼭대기에서 대기
            yield return new WaitForSeconds(waitDuration);

            // 들어가기
            yield return MoveTo(hidePosition.position);
        }
    }

    private IEnumerator MoveTo(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            while (GameState.IsVictory) yield return null;
            transform.position = Vector3.MoveTowards(
                transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    private bool IsPlayerNear()
    {
        if (player == null)
        {
            var obj = GameObject.FindGameObjectWithTag(playerTag);
            if (obj != null) player = obj.transform;
        }
        if (player == null) return false;

        return Mathf.Abs(player.position.x - transform.position.x) < playerNearRadius;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        other.GetComponent<PlayerController>()?.TakeDamage();
    }
}
