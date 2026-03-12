using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Play scene entry point.
/// Reads GameState.SelectedMapId, loads the JSON, restores tilemaps and spawn position.
/// </summary>
public class PlaySceneController : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap oneWayTilemap;
    [SerializeField] private Tilemap backgroundTilemap;
    [SerializeField] private Tilemap gimmickTilemap;
    [SerializeField] private Tilemap hazardTilemap;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Settings")]
    [SerializeField] private string resourcesPalettePath = "Tiles";
    [SerializeField] private string saveSubFolder = "Maps";
    [SerializeField] private string mapListSceneName = "MapList";

    private List<TilePaletteEntry> palette = new List<TilePaletteEntry>();

    private void Start()
    {
        if (string.IsNullOrEmpty(GameState.SelectedMapId))
        {
            Debug.LogWarning("[PlaySceneController] SelectedMapId is empty. No map to load.");
            return;
        }

        LoadPalette();
        LoadMap();

        if (cameraFollow != null) cameraFollow.enabled = true;
    }

    private void LoadPalette()
    {
        palette.Clear();
        string basePath = resourcesPalettePath.Trim('/');

        var folders = new[]
        {
            ("Ground",     TileLayerType.Solid),
            ("OneWay",     TileLayerType.OneWay),
            ("Background", TileLayerType.BackGround),
            ("Gimmick",    TileLayerType.Gimmick),
            ("Hazard",     TileLayerType.Hazard)
        };

        foreach (var (folderName, layer) in folders)
        {
            string path = string.IsNullOrEmpty(basePath) ? folderName : basePath + "/" + folderName;
            TileBase[] tiles = Resources.LoadAll<TileBase>(path);
            if (tiles == null || tiles.Length == 0)
            {
                string pathLower = basePath + "/" + folderName.ToLowerInvariant();
                tiles = Resources.LoadAll<TileBase>(pathLower);
            }
            if (tiles == null || tiles.Length == 0) continue;
            foreach (TileBase t in tiles)
                palette.Add(new TilePaletteEntry { id = t.name, tile = t, layer = layer });
        }

        if (palette.Count == 0)
        {
            TileBase[] tiles = Resources.LoadAll<TileBase>(basePath);
            if (tiles != null)
                foreach (TileBase t in tiles)
                    palette.Add(new TilePaletteEntry { id = t.name, tile = t, layer = TileLayerType.Solid });
        }

        Debug.Log($"[PlaySceneController] Palette loaded: {palette.Count} tiles.");
    }

    private void LoadMap()
    {
        string dir = Path.Combine(Application.persistentDataPath, saveSubFolder);
        string path = Path.Combine(dir, GameState.SelectedMapId + ".json");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[PlaySceneController] Map file not found: {path}");
            return;
        }

        string json = File.ReadAllText(path);
        MapData data = JsonUtility.FromJson<MapData>(json);
        ApplyMapData(data);
        Debug.Log($"[PlaySceneController] Map loaded: {GameState.SelectedMapId}");
    }

    private void ApplyMapData(MapData data)
    {
        if (data == null) return;

        // 타일맵 클리어
        groundTilemap.ClearAllTiles();
        if (oneWayTilemap != null) oneWayTilemap.ClearAllTiles();
        if (backgroundTilemap != null) backgroundTilemap.ClearAllTiles();
        if (gimmickTilemap != null) gimmickTilemap.ClearAllTiles();
        if (hazardTilemap != null) hazardTilemap.ClearAllTiles();

        // 레이어별 타일 복원
        ApplyLayerData(data.groundCells, groundTilemap);
        ApplyLayerData(data.oneWayCells, oneWayTilemap != null ? oneWayTilemap : groundTilemap);
        ApplyLayerData(data.backgroundCells, backgroundTilemap != null ? backgroundTilemap : groundTilemap);
        ApplyLayerData(data.gimmickCells, gimmickTilemap != null ? gimmickTilemap : groundTilemap);
        ApplyLayerData(data.hazardCells, hazardTilemap != null ? hazardTilemap : groundTilemap);

        // 플레이어 스폰 위치 적용
        if (player != null)
        {
            var pos = player.position;
            pos.x = data.spawnX;
            pos.y = data.spawnY;
            player.position = pos;
        }
        else
        {
            Debug.LogWarning("[PlaySceneController] Player is not assigned. Spawn position not applied.");
        }
    }

    private void ApplyLayerData(List<TileCellData> cells, Tilemap tilemap)
    {
        if (cells == null || tilemap == null) return;
        foreach (var cell in cells)
        {
            TilePaletteEntry entry = palette.Find(e => e.id == cell.tileId);
            if (entry == null)
            {
                Debug.LogWarning($"[PlaySceneController] Tile not found in palette: {cell.tileId}");
                continue;
            }
            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), entry.tile);
        }
    }

    public void OnToMapListClicked()
    {
        if (!string.IsNullOrEmpty(mapListSceneName))
            SceneManager.LoadScene(mapListSceneName);
    }
}
