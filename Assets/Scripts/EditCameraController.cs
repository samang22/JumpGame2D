using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Edit 모드 전용 카메라 조작. CameraFollow가 꺼져 있을 때만 동작.
/// WASD / 방향키: 이동 | 스크롤: 줌
/// </summary>
public class EditCameraController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 20f;

    private CameraFollow _cameraFollow;

    private void Awake()
    {
        _cameraFollow = GetComponent<CameraFollow>();
    }

    private void Update()
    {
        if (_cameraFollow != null && _cameraFollow.enabled) return;

        HandleMove();
        HandleZoom();
    }

    private void HandleMove()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float x = 0f, y = 0f;

        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;

        if (x == 0f && y == 0f) return;

        float speed = moveSpeed * Camera.main.orthographicSize / 5f;
        transform.position += new Vector3(x, y, 0f) * speed * Time.deltaTime;
    }

    private void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll == 0f) return;

        Camera cam = Camera.main;
        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize - scroll * zoomSpeed * Time.deltaTime,
            minZoom, maxZoom
        );
    }
}
