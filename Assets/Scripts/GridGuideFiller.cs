using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Grid Guide 타일맵을 지정한 영역만큼 한 번에 채우기 위한 설정.
/// 에디터에서 "Fill Grid Guide" 버튼으로 실행.
/// </summary>
public class GridGuideFiller : MonoBehaviour
{
    [Tooltip("그리드 가이드로 사용할 Tilemap (예: Tilemap_GridGuide)")]
    public Tilemap gridGuideTilemap;

    [Tooltip("가이드 셀에 넣을 Tile (테두리만 있는 스프라이트 타일)")]
    public TileBase gridGuideTile;

    [Tooltip("채울 영역: Min (셀 좌표). 예: (-50, -50, 0)")]
    public Vector3Int fillBoundsMin = new Vector3Int(-50, -50, 0);

    [Tooltip("채울 영역: Size (가로, 세로, 1). 예: (100, 100, 1)")]
    public Vector3Int fillBoundsSize = new Vector3Int(100, 100, 1);

    /// <summary>
    /// 다른 타일맵의 사용 영역(cellBounds)을 이 filler의 fillBoundsMin/Size에 반영할 때 사용.
    /// </summary>
    public void SetBoundsFromTilemap(Tilemap other)
    {
        if (other == null) return;
        var b = other.cellBounds;
        fillBoundsMin = new Vector3Int(b.xMin, b.yMin, 0);
        fillBoundsSize = new Vector3Int(b.size.x, b.size.y, 1);
    }

    public BoundsInt GetFillBounds()
    {
        return new BoundsInt(fillBoundsMin, fillBoundsSize);
    }
}
