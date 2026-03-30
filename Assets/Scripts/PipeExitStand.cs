using UnityEngine;

/// <summary>
/// 출구 파이프 프리팹: 플레이어가 워프 후 설 위치. 비우면 루트 Transform 사용.
/// </summary>
public class PipeExitStand : MonoBehaviour
{
    [SerializeField] private Transform standPoint;

    public Transform GetStandPoint() => standPoint != null ? standPoint : transform;
}
