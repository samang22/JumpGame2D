using UnityEngine;

/// <summary>
/// Makes the camera follow a target. Enable during test play, disable in edit mode.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float smoothTime = 0.2f;

    private Vector3 _velocity;

    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, smoothTime);
    }
}
