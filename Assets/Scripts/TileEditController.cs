using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TileEditController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera editCamera;
    [Tooltip("Player or spawn marker Transform. Position is saved as spawn in MapData.")]
    [SerializeField] private GameObject spawnMarker;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap oneWayTilemap;
    [SerializeField] private Tilemap backgroundTilemap;
    [SerializeField] private Tilemap gimmickTilemap;
    [SerializeField] private Tilemap hazardTilemap;

    [Header("Palette")]
    [SerializeField] private List<TilePaletteEntry> palette;
    [SerializeField] private string resourcesPalettePath = "Tiles";
    [SerializeField] private TilePaletteUI paletteUI;

    [Header("Current State")]
    [SerializeField] private TileBase paintTile;    // currently selected tile for painting
    [SerializeField] private string paintTileId;    // ID of the currently selected tile (for save/load)
    [SerializeField] private bool eraseMode;        // true = erase instead of paint

    private Tilemap GetTilemapForLayer(TileLayerType layer)
    {
        switch (layer)
        {
            case TileLayerType.OneWay:
                return oneWayTilemap != null ? oneWayTilemap : groundTilemap;
            case TileLayerType.BackGround:
                return backgroundTilemap != null ? backgroundTilemap : groundTilemap;
            case TileLayerType.Gimmick:
                return gimmickTilemap != null ? gimmickTilemap : groundTilemap;
            case TileLayerType.Hazard:
                return hazardTilemap != null ? hazardTilemap : groundTilemap;
            default:
                return groundTilemap;
        }
    }

    void Update()
    {
        if (editCamera == null || groundTilemap == null) return;
        if (Mouse.current == null) return;

        // Don't paint/erase when clicking on UI (e.g. Tile List button, palette buttons)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3Int cellPos = GetMouseCell(mouseScreenPos);

        bool leftPressed = Mouse.current.leftButton.isPressed;
        bool rightPressed = Mouse.current.rightButton.isPressed;

        // 플레이어(스폰) 위 클릭 시 타일 그리기/지우기 하지 않음 (드래그 배치와 겹침 방지)
        if (spawnMarker != null)
        {
            Vector3 w = editCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
            var hit = Physics2D.OverlapPoint(new Vector2(w.x, w.y));
            if (hit != null && spawnMarker != null &&
               (hit.transform == spawnMarker.transform || hit.transform.IsChildOf(spawnMarker.transform)))
            {
                return;
            }
        }

        if (leftPressed)
        {
            if (eraseMode)
                EraseAt(cellPos);
            else
                PaintAt(cellPos);
        }
        else if (rightPressed)
        {
            EraseAt(cellPos);
        }
    }

    Vector3Int GetMouseCell(Vector2 mouseScreenPos)
    {
        Vector3 worldPos = editCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
        return groundTilemap.WorldToCell(worldPos);
    }

    void PaintAt(Vector3Int cell)
    {
        if (paintTile == null) return;

        TilePaletteEntry entry = FindEntryByTile(paintTile);
        Tilemap target = entry != null ? GetTilemapForLayer(entry.layer) : groundTilemap;
        target.SetTile(cell, paintTile);
    }

    void EraseAt(Vector3Int cell)
    {
        groundTilemap.SetTile(cell, null);
        if (oneWayTilemap != null) oneWayTilemap.SetTile(cell, null);
        if (backgroundTilemap != null) backgroundTilemap.SetTile(cell, null);
        if (gimmickTilemap != null) gimmickTilemap.SetTile(cell, null);
        if (hazardTilemap != null) hazardTilemap.SetTile(cell, null);
    }

    public void SetPaintTile(TileBase tile)
    {
        paintTile = tile;
        paintTileId = FindEntryByTile(tile)?.id ?? "";
    }

    public void SetPaintTileById(string id)
    {
        var entry = FindEntryById(id);
        if (entry == null) return;
        paintTile = entry.tile;
        paintTileId = id;
    }

    public void SetEraseMode(bool on) { eraseMode = on; }

    /// <summary>Returns the current palette list. Creates an empty list if needed.</summary>
    public IReadOnlyList<TilePaletteEntry> GetPalette() => palette ?? (palette = new List<TilePaletteEntry>());

    /// <summary>Loads tiles from Resources into the palette. 서브폴더명으로 레이어 자동 지정 (Ground, Gimmick 등).</summary>
    public void LoadPaletteFromResources()
    {
        if (string.IsNullOrWhiteSpace(resourcesPalettePath))
        {
            Debug.LogWarning("Resources palette path is empty.");
            return;
        }

        string basePath = resourcesPalettePath.Trim('/');
        if (palette == null) palette = new List<TilePaletteEntry>();
        palette.Clear();

        // 서브폴더별로 로드해 레이어 지정 (섹션 구분 유지). 폴더명 대소문자 여러 형태 시도.
        var folders = new[] { ("Ground", TileLayerType.Solid), ("OneWay", TileLayerType.OneWay), ("Background", TileLayerType.BackGround), ("Gimmick", TileLayerType.Gimmick), ("Hazard", TileLayerType.Hazard) };
        foreach (var (folderName, layer) in folders)
        {
            string path = string.IsNullOrEmpty(basePath) ? folderName : basePath + "/" + folderName;
            TileBase[] tiles = Resources.LoadAll<TileBase>(path);
            if (tiles == null || tiles.Length == 0 && !string.IsNullOrEmpty(basePath))
            {
                string pathLower = basePath + "/" + folderName.ToLowerInvariant();
                tiles = Resources.LoadAll<TileBase>(pathLower);
            }
            if (tiles == null || tiles.Length == 0) continue;
            foreach (TileBase t in tiles)
            {
                palette.Add(new TilePaletteEntry
                {
                    id = t.name,
                    displayName = t.name,
                    icon = null,
                    tile = t,
                    layer = layer
                });
            }
        }

        // 서브폴더에 없으면 루트 경로에서 로드 (전부 Solid)
        if (palette.Count == 0)
        {
            TileBase[] tiles = Resources.LoadAll<TileBase>(basePath);
            if (tiles != null)
            {
                foreach (TileBase t in tiles)
                {
                    palette.Add(new TilePaletteEntry
                    {
                        id = t.name,
                        displayName = t.name,
                        icon = null,
                        tile = t,
                        layer = TileLayerType.Solid
                    });
                }
            }
        }
    }

    TilePaletteEntry FindEntryByTile(TileBase tile)
    {
        if (tile == null || palette == null) return null;
        foreach (var e in palette)
            if (e.tile == tile) return e;
        return null;
    }

    TilePaletteEntry FindEntryById(string id)
    {
        if (string.IsNullOrEmpty(id) || palette == null) return null;
        foreach (var e in palette)
            if (e.id == id) return e;
        return null;
    }

    public string GetTileIdAt(Vector3Int cell)
    {
        TileBase t = groundTilemap.GetTile(cell);
        if (t == null && oneWayTilemap != null) t = oneWayTilemap.GetTile(cell);
        if (t == null && backgroundTilemap != null) t = backgroundTilemap.GetTile(cell);
        if (t == null) return null;
        var e = FindEntryByTile(t);
        return e?.id;
    }

    public TileBase GetTileAt(Vector3Int cell)
    {
        TileBase t = groundTilemap.GetTile(cell);
        if (t != null) return t;
        if (oneWayTilemap != null)
        {
            t = oneWayTilemap.GetTile(cell);
            if (t != null) return t;
        }
        if (backgroundTilemap != null)
        {
            t = backgroundTilemap.GetTile(cell);
            if (t != null) return t;
        }
        if (gimmickTilemap != null)
        {
            t = gimmickTilemap.GetTile(cell);
            if (t != null) return t;
        }
        if (hazardTilemap != null)
        {
            t = hazardTilemap.GetTile(cell);
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>Clears all tilemaps and applies MapData (tiles + spawn position).</summary>
    public void ApplyMapData(MapData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[TileEditController] ApplyMapData: data is null.");
            return;
        }

        // 1) 타일맵 전부 클리어
        groundTilemap.ClearAllTiles();
        if (oneWayTilemap != null) oneWayTilemap.ClearAllTiles();
        if (backgroundTilemap != null) backgroundTilemap.ClearAllTiles();
        if (gimmickTilemap != null) gimmickTilemap.ClearAllTiles();
        if (hazardTilemap != null) hazardTilemap.ClearAllTiles();

        // 2) 팔레트가 비어 있으면 먼저 로드
        if (palette == null || palette.Count == 0)
            LoadPaletteFromResources();

        // 3) 각 레이어 복원
        ApplyLayerData(data.groundCells, groundTilemap);
        ApplyLayerData(data.oneWayCells, oneWayTilemap != null ? oneWayTilemap : groundTilemap);
        ApplyLayerData(data.backgroundCells, backgroundTilemap != null ? backgroundTilemap : groundTilemap);
        ApplyLayerData(data.gimmickCells, gimmickTilemap != null ? gimmickTilemap : groundTilemap);
        ApplyLayerData(data.hazardCells, hazardTilemap != null ? hazardTilemap : groundTilemap);

        // 4) 스폰마커 위치 복원
        if (spawnMarker != null)
        {
            var pos = spawnMarker.transform.position;
            pos.x = data.spawnX;
            pos.y = data.spawnY;
            spawnMarker.transform.position = pos;
        }
    }

    private void ApplyLayerData(List<TileCellData> cells, Tilemap tilemap)
    {
        if (cells == null || tilemap == null) return;
        foreach (var cell in cells)
        {
            var entry = FindEntryById(cell.tileId);
            if (entry == null)
            {
                Debug.LogWarning($"[TileEditController] Tile not found in palette: {cell.tileId}");
                continue;
            }
            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), entry.tile);
        }
    }

    /// <summary>Builds MapData from current tilemap state. Use when starting Test Play or saving.</summary>
    public MapData CollectMapData()
    {
        var data = new MapData();
        if (spawnMarker == null)
            return data;

        var spawnT = spawnMarker.transform;
        if (spawnT != null)
        {
            data.spawnX = spawnT.position.x;
            data.spawnY = spawnT.position.y;
        }
        FillLayerData(groundTilemap, data.groundCells);
        if (oneWayTilemap != null) FillLayerData(oneWayTilemap, data.oneWayCells);
        if (backgroundTilemap != null) FillLayerData(backgroundTilemap, data.backgroundCells);
        if (gimmickTilemap != null) FillLayerData(gimmickTilemap, data.gimmickCells);
        if (hazardTilemap != null) FillLayerData(hazardTilemap, data.hazardCells);
        return data;
    }

    private void FillLayerData(Tilemap tilemap, List<TileCellData> list)
    {
        if (tilemap == null || list == null) return;
        list.Clear();
        var bounds = tilemap.cellBounds;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                TileBase t = tilemap.GetTile(cell);
                if (t == null) continue;
                var entry = FindEntryByTile(t);
                list.Add(new TileCellData { x = x, y = y, tileId = entry != null ? entry.id : t.name });
            }
        }
    }
    
    public void ClearPaintSelection()
    {
        paintTile = null;
        paintTileId = "";
        eraseMode = false;
    }
}