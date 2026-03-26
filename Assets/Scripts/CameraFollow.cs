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
    private PlayerController _player;

    private void Awake()
    {
        if (target != null)
            _player = target.GetComponent<PlayerController>() ?? target.GetComponentInParent<PlayerController>();
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            Debug.LogWarning("[CameraFollow] target is null!");
            return;
        }
        _velocity = Vector3.zero;
        transform.position = target.position + offset;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (_player != null && _player.IsDead) return;

        Vector3 goal = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, smoothTime);
    }
}
