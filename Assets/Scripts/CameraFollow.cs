using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target; // Player
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

    [Header("Dead Zone Settings")]
    [SerializeField] private float deadZoneWidth = 2f; // 좌우 Dead Zone 크기 (World 단위)
    [SerializeField] private float deadZoneHeight = 1f; // 상하 Dead Zone 크기

    [Header("Smooth Settings")]
    [SerializeField] private float smoothSpeed = 0.125f;
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = false;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = transform.position;

        // X축 Dead Zone 체크
        if (followX)
        {
            float deltaX = target.position.x - transform.position.x;

            if (Mathf.Abs(deltaX) > deadZoneWidth / 2f)
            {
                // Dead Zone 밖으로 나간 경우에만 카메라 이동
                if (deltaX > 0)
                    desiredPosition.x = target.position.x - deadZoneWidth / 2f;
                else
                    desiredPosition.x = target.position.x + deadZoneWidth / 2f;
            }
        }

        // Y축 Dead Zone 체크
        if (followY)
        {
            float deltaY = target.position.y - transform.position.y;

            if (Mathf.Abs(deltaY) > deadZoneHeight / 2f)
            {
                // Dead Zone 밖으로 나간 경우에만 카메라 이동
                if (deltaY > 0)
                    desiredPosition.y = target.position.y - deadZoneHeight / 2f;
                else
                    desiredPosition.y = target.position.y + deadZoneHeight / 2f;
            }
        }

        // Z축은 offset 유지
        desiredPosition.z = target.position.z + offset.z;

        // 부드럽게 이동
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    // Scene 뷰에서 Dead Zone 시각화 (에디터 전용)
    void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Gizmos.color = Color.red;
        Vector3 center = transform.position;
        center.z = target.position.z;

        // Dead Zone 박스 그리기
        Gizmos.DrawWireCube(center, new Vector3(deadZoneWidth, deadZoneHeight, 0));
    }
}