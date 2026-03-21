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
    [Tooltip("Goal marker GameObject. Position is saved as goal in MapData.")]
    [SerializeField] private GameObject goalMarker;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap oneWayTilemap;
    [SerializeField] private Tilemap backgroundTilemap;
    [SerializeField] private Tilemap gimmickTilemap;
    [SerializeField] private Tilemap hazardTilemap;

    [Header("Palette")]
    [SerializeField] private List<TilePaletteEntry> palette;
    [SerializeField] private string resourcesPalettePath = "Tiles";
    [SerializeField] private TilePaletteUI paletteUI;

    [Header("Power-ups (palette UI)")]
    [Tooltip("버섯/꽃 등 프리팹. id는 저장 JSON의 prefabId와 동일해야 Play 씬에서도 복원됨.")]
    [SerializeField] private List<PowerUpPaletteEntry> powerUpPalette;
    [Tooltip("에디트에서 배치한 파워업의 부모. 비어 있으면 배치·저장이 동작하지 않음.")]
    [SerializeField] private Transform placedPowerUpsRoot;

    [Header("Current State")]
    [SerializeField] private TileBase paintTile;    // currently selected tile for painting
    [SerializeField] private string paintTileId;    // ID of the currently selected tile (for save/load)
    [SerializeField] private bool eraseMode;        // true = erase instead of paint

    /// <summary>선택된 파워업 팔레트 id. 비어 있지 않으면 클릭 시 해당 프리팹을 셀 중앙에 배치.</summary>
    private string selectedPowerUpId = "";

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

        // 스폰 마커 위에서는 타일 칠/지우기만 막음. 파워업 배치는 허용.
        bool pointerOverSpawn = false;
        if (spawnMarker != null)
        {
            Vector3 w = editCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
            var hit = Physics2D.OverlapPoint(new Vector2(w.x, w.y));
            if (hit != null &&
               (hit.transform == spawnMarker.transform || hit.transform.IsChildOf(spawnMarker.transform)))
                pointerOverSpawn = true;
        }

        // 파워업: 마우스 업(버튼을 뗀 프레임)에만 설치 — 뗄 때 커서가 있는 셀에 배치
        if (Mouse.current.leftButton.wasReleasedThisFrame && !eraseMode && !string.IsNullOrEmpty(selectedPowerUpId))
        {
            if (!pointerOverSpawn)
                PlacePowerUpAtCell(cellPos);
        }

        if (leftPressed)
        {
            if (eraseMode)
            {
                if (!pointerOverSpawn)
                    EraseAt(cellPos);
                ErasePowerUpsAtCell(cellPos);
            }
            else if (paintTile != null)
            {
                if (!pointerOverSpawn)
                    PaintAt(cellPos);
            }
        }
        else if (rightPressed)
        {
            if (!pointerOverSpawn)
                EraseAt(cellPos);
            ErasePowerUpsAtCell(cellPos);
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
        selectedPowerUpId = "";
    }

    public void SetPaintTileById(string id)
    {
        var entry = FindEntryById(id);
        if (entry == null) return;
        paintTile = entry.tile;
        paintTileId = id;
        selectedPowerUpId = "";
    }

    public void SetEraseMode(bool on)
    {
        eraseMode = on;
        if (on) selectedPowerUpId = "";
    }

    /// <summary>타일 팔레트에서 파워업 버튼 클릭 시 호출. id는 PowerUpPaletteEntry.id와 동일.</summary>
    public void SetPlacePowerUpById(string id)
    {
        if (string.IsNullOrEmpty(id) || powerUpPalette == null)
        {
            selectedPowerUpId = "";
            return;
        }
        foreach (var e in powerUpPalette)
        {
            if (e == null || e.prefab == null) continue;
            if (e.id != id) continue;
            selectedPowerUpId = id;
            paintTile = null;
            paintTileId = "";
            eraseMode = false;
            return;
        }
        selectedPowerUpId = "";
    }

    public void ClearPowerUpSelection() => selectedPowerUpId = "";

    /// <summary>TilePaletteUI에서 파워업 섹션을 그릴 때 사용.</summary>
    public IReadOnlyList<PowerUpPaletteEntry> GetPowerUpPalette() => powerUpPalette ?? (powerUpPalette = new List<PowerUpPaletteEntry>());

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

        // 5) 골마커 위치 복원
        if (goalMarker != null)
        {
            var pos = goalMarker.transform.position;
            pos.x = data.goalX;
            pos.y = data.goalY;
            goalMarker.transform.position = pos;
        }

        // 6) 파워업 배치 복원
        if (data.powerUps == null) data.powerUps = new List<PlacedPowerUpData>();
        ClearPlacedPowerUps();
        ApplyPowerUpsFromData(data.powerUps);
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
            Debug.LogWarning("[TileEditController] spawnMarker is not assigned. Spawn position will not be saved.");
        else
        {
            data.spawnX = spawnMarker.transform.position.x;
            data.spawnY = spawnMarker.transform.position.y;
        }

        if (goalMarker == null)
            Debug.LogWarning("[TileEditController] goalMarker is not assigned. Goal position will not be saved.");
        else
        {
            data.goalX = goalMarker.transform.position.x;
            data.goalY = goalMarker.transform.position.y;
        }

        FillLayerData(groundTilemap, data.groundCells);
        if (oneWayTilemap != null) FillLayerData(oneWayTilemap, data.oneWayCells);
        if (backgroundTilemap != null) FillLayerData(backgroundTilemap, data.backgroundCells);
        if (gimmickTilemap != null) FillLayerData(gimmickTilemap, data.gimmickCells);
        if (hazardTilemap != null) FillLayerData(hazardTilemap, data.hazardCells);

        data.powerUps = new List<PlacedPowerUpData>();
        if (placedPowerUpsRoot != null)
        {
            foreach (var m in placedPowerUpsRoot.GetComponentsInChildren<PlacedPowerUpEditMarker>(true))
            {
                var t = m.transform.position;
                data.powerUps.Add(new PlacedPowerUpData
                {
                    prefabId = m.paletteId,
                    x = t.x,
                    y = t.y
                });
            }
        }

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
        selectedPowerUpId = "";
    }

    void PlacePowerUpAtCell(Vector3Int cell)
    {
        if (placedPowerUpsRoot == null) return;
        var entry = FindPowerUpEntryById(selectedPowerUpId);
        if (entry == null || entry.prefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell);
        pos.z = 0f;
        var go = Instantiate(entry.prefab, pos, Quaternion.identity, placedPowerUpsRoot);
        var marker = go.GetComponent<PlacedPowerUpEditMarker>();
        if (marker == null) marker = go.AddComponent<PlacedPowerUpEditMarker>();
        marker.paletteId = entry.id;
    }

    void ErasePowerUpsAtCell(Vector3Int cell)
    {
        if (placedPowerUpsRoot == null) return;
        Bounds b = new Bounds(groundTilemap.GetCellCenterWorld(cell), (Vector3)groundTilemap.cellSize);
        var markers = placedPowerUpsRoot.GetComponentsInChildren<PlacedPowerUpEditMarker>(true);
        foreach (var m in markers)
        {
            if (m != null && b.Contains(m.transform.position))
                Destroy(m.gameObject);
        }
    }

    void ClearPlacedPowerUps()
    {
        if (placedPowerUpsRoot == null) return;
        for (int i = placedPowerUpsRoot.childCount - 1; i >= 0; i--)
            Destroy(placedPowerUpsRoot.GetChild(i).gameObject);
    }

    void ApplyPowerUpsFromData(List<PlacedPowerUpData> list)
    {
        if (list == null || placedPowerUpsRoot == null) return;
        foreach (var p in list)
        {
            if (p == null || string.IsNullOrEmpty(p.prefabId)) continue;
            var entry = FindPowerUpEntryById(p.prefabId);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[TileEditController] Power-up prefab not in palette: {p.prefabId}");
                continue;
            }
            var go = Instantiate(entry.prefab, new Vector3(p.x, p.y, 0f), Quaternion.identity, placedPowerUpsRoot);
            var marker = go.GetComponent<PlacedPowerUpEditMarker>();
            if (marker == null) marker = go.AddComponent<PlacedPowerUpEditMarker>();
            marker.paletteId = entry.id;
        }
    }

    PowerUpPaletteEntry FindPowerUpEntryById(string id)
    {
        if (string.IsNullOrEmpty(id) || powerUpPalette == null) return null;
        foreach (var e in powerUpPalette)
            if (e != null && e.id == id) return e;
        return null;
    }
}