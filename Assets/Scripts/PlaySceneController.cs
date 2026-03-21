using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;

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
    [Tooltip("GoalMarker 프리팹. 맵 로드 시 goalX/Y 위치에 자동 생성됨.")]
    [SerializeField] private GoalMarker goalMarkerPrefab;

    [Header("Clear UI")]
    [SerializeField] private GameObject clearPanel;
    [SerializeField] private TMP_Text clearTimeText;

    [Header("Timer")]
    [SerializeField] private GameTimer gameTimer;
    [SerializeField] private GameObject timerTextObject;

    [Header("Power-ups (맵 JSON의 prefabId와 동일한 id로 등록)")]
    [SerializeField] private List<PowerUpPaletteEntry> powerUpPalette = new List<PowerUpPaletteEntry>();
    [Tooltip("플레이 씬에서 생성된 파워업의 부모. 비어 있으면 씬 루트에 생성.")]
    [SerializeField] private Transform powerUpsRoot;

    [Header("Settings")]
    [SerializeField] private string resourcesPalettePath = "Tiles";
    [SerializeField] private string saveSubFolder = "Maps";
    [SerializeField] private string mapListSceneName = "MapList";

    private List<TilePaletteEntry> palette = new List<TilePaletteEntry>();
    private GoalMarker goalMarker;

    private void Awake()
    {
        GameState.IsMapEditMode = false;
    }

    private void Start()
    {
        if (clearPanel != null) clearPanel.SetActive(false);

        if (string.IsNullOrEmpty(GameState.SelectedMapId))
        {
            Debug.LogWarning("[PlaySceneController] SelectedMapId is empty. No map to load.");
            return;
        }

        LoadPalette();
        LoadMap();

        if (cameraFollow != null) cameraFollow.enabled = true;
        if (gameTimer != null) gameTimer.StartTimer();
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
            Vector3 spawnPos = new Vector3(data.spawnX, data.spawnY, player.position.z);
            player.position = spawnPos;

            var playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
                playerController.SetSpawnPosition(spawnPos);
        }
        else
        {
            Debug.LogWarning("[PlaySceneController] Player is not assigned. Spawn position not applied.");
        }

        // 골 마커 생성
        if (goalMarkerPrefab != null)
        {
            if (goalMarker != null)
                Destroy(goalMarker.gameObject);

            Vector3 spawnPos = new Vector3(data.goalX, data.goalY, 0f);
            goalMarker = Instantiate(goalMarkerPrefab, spawnPos, Quaternion.identity);
            goalMarker.ResetGoal();
            goalMarker.OnGoalReached += OnGoalReached;
        }
        else
        {
            Debug.LogWarning("[PlaySceneController] goalMarkerPrefab is not assigned.");
        }

        ApplyPowerUpsFromMapData(data);
    }

    private void ApplyPowerUpsFromMapData(MapData data)
    {
        if (powerUpsRoot != null)
        {
            for (int i = powerUpsRoot.childCount - 1; i >= 0; i--)
                Destroy(powerUpsRoot.GetChild(i).gameObject);
        }

        if (data == null || data.powerUps == null || data.powerUps.Count == 0) return;

        foreach (var p in data.powerUps)
        {
            if (p == null || string.IsNullOrEmpty(p.prefabId)) continue;
            var entry = FindPowerUpPaletteEntry(p.prefabId);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[PlaySceneController] Power-up id not in palette: {p.prefabId}");
                continue;
            }
            Instantiate(entry.prefab, new Vector3(p.x, p.y, 0f), Quaternion.identity, powerUpsRoot);
        }
    }

    private PowerUpPaletteEntry FindPowerUpPaletteEntry(string id)
    {
        if (string.IsNullOrEmpty(id) || powerUpPalette == null) return null;
        foreach (var e in powerUpPalette)
            if (e != null && e.id == id) return e;
        return null;
    }

    private void OnGoalReached()
    {
        if (gameTimer != null) gameTimer.StopTimer();
        if (timerTextObject != null) timerTextObject.SetActive(false);

        if (clearPanel != null)
            clearPanel.SetActive(true);

        if (clearTimeText != null && gameTimer != null)
            clearTimeText.text = $"Time: {gameTimer.GetFormattedTime()}";

        var playerController = player != null ? player.GetComponent<PlayerController>() : null;
        if (playerController != null)
            playerController.enabled = false;

        StartCoroutine(ReturnToMapListRoutine());
    }

    [SerializeField] private float clearToMapListDelay = 2f;

    private IEnumerator ReturnToMapListRoutine()
    {
        yield return new WaitForSeconds(clearToMapListDelay);
        if (!string.IsNullOrEmpty(mapListSceneName))
            SceneManager.LoadScene(mapListSceneName);
    }

    public void OnClearBackToMapList()
    {
        StopCoroutine(nameof(ReturnToMapListRoutine));
        if (!string.IsNullOrEmpty(mapListSceneName))
            SceneManager.LoadScene(mapListSceneName);
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
