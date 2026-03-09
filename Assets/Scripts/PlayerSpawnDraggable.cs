using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

/// <summary>
/// 에디트 모드에서 플레이어(스폰)를 드래그로 배치. 그리드 셀에 스냅.
/// Test Play 중에는 비활성.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerSpawnDraggable : MonoBehaviour
{
    [Tooltip("스냅 기준 Grid. 비면 부모에서 찾음.")]
    [SerializeField] private Grid grid;
    [Tooltip("스냅 기준 Tilemap (셀 크기용). 비면 Grid 자식 Tilemap 사용.")]
    [SerializeField] private Tilemap tilemap;

    private bool _dragging;
    private Vector3 _dragOffset;
    private Camera _camera;

    private void Awake()
    {
        if (grid == null) grid = GetComponentInParent<Grid>();
        if (tilemap == null && grid != null)
        {
            var tm = grid.GetComponentInChildren<Tilemap>();
            if (tm != null) tilemap = tm;
        }
        var cam = Camera.main;
        if (cam != null) _camera = cam;
    }

    private void OnEnable()
    {
        if (GameState.IsTestPlay)
        {
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (GameState.IsTestPlay) return;

        if (Mouse.current == null) return;
        Vector2 screen = Mouse.current.position.ReadValue();

        if (_dragging)
        {
            if (!Mouse.current.leftButton.isPressed)
            {
                _dragging = false;
                return;
            }
            if (_camera != null && (tilemap != null || grid != null))
            {
                Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
                world.z = transform.position.z;
                Vector3 snapped = SnapToGrid(world);
                transform.position = snapped + _dragOffset;
            }
            return;
        }

        // 새 Input System에서는 OnMouseDown이 안 불릴 수 있어, 클릭 시 레이로 확인
        if (Mouse.current.leftButton.wasPressedThisFrame && _camera != null)
        {
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            var hit = Physics2D.OverlapPoint(new Vector2(world.x, world.y));
            if (hit != null && (hit.transform == transform || hit.transform.IsChildOf(transform)))
            {
                world.z = transform.position.z;
                _dragOffset = transform.position - SnapToGrid(world);
                _dragging = true;
            }
        }
    }

    private void OnMouseDown()
    {
        if (GameState.IsTestPlay) return;
        if (_camera == null) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
        world.z = transform.position.z;
        _dragOffset = transform.position - SnapToGrid(world);
        _dragging = true;
    }

    private Vector3 SnapToGrid(Vector3 worldPos)
    {
        if (tilemap != null)
        {
            var cell = tilemap.WorldToCell(worldPos);
            return tilemap.GetCellCenterWorld(cell);
        }
        if (grid != null)
        {
            var cell = grid.WorldToCell(worldPos);
            return grid.GetCellCenterWorld(cell);
        }
        return worldPos;
    }
}
