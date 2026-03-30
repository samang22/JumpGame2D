using System;
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
    [Tooltip("비어 있지 않으면 Resources 로드 대신 이 레지스트리만 사용 (에디터에서 바로 지정할 때).")]
    [SerializeField] private MonsterPaletteRegistry monsterRegistryOverride;
    [Tooltip("Resources 안의 MonsterPaletteRegistry 에셋 이름. 확장자 없음 (예: MonsterPaletteRegistry).")]
    [SerializeField] private string monsterRegistryResourcePath = "MonsterPaletteRegistry";
    [SerializeField] private TilePaletteUI paletteUI;

    [Header("Power-ups (palette UI)")]
    [Tooltip("버섯/꽃 등 프리팹. id는 저장 JSON의 prefabId와 동일해야 Play 씬에서도 복원됨.")]
    [SerializeField] private List<PowerUpPaletteEntry> powerUpPalette;
    [Tooltip("에디트에서 배치한 파워업의 부모. 비어 있으면 배치·저장이 동작하지 않음.")]
    [SerializeField] private Transform placedPowerUpsRoot;

    [Header("Pipes — 입구 / 출구 별도 프리팹 (pairId로 짝 지음)")]
    [SerializeField] private List<PipePaletteEntry> pipeEntrancePalette;
    [SerializeField] private List<PipePaletteEntry> pipeExitPalette;
    [Tooltip("입구 파이프 인스턴스 부모")]
    [SerializeField] private Transform placedPipeEntrancesRoot;
    [Tooltip("출구 파이프 인스턴스 부모")]
    [SerializeField] private Transform placedPipeExitsRoot;
    [Tooltip("파이프 입·출구 배치 시 셀 중심에서 세로 방향으로 이만큼 ‘칸’ 단위 이동 (+면 위). 기본 0.5 = 반 칸.")]
    [SerializeField] private float pipePlacementVerticalOffsetCells = 0.5f;

    [Header("Question blocks (palette UI)")]
    [Tooltip("물음표 블록 프리팹. id는 저장 JSON의 prefabId와 동일해야 Play 씬에서도 복원됨.")]
    [SerializeField] private List<QuestionBlockPaletteEntry> questionBlockPalette;
    [Tooltip("에디트에서 배치한 물음표 블록의 부모. 비어 있으면 배치·저장이 동작하지 않음.")]
    [SerializeField] private Transform placedQuestionBlocksRoot;

    [Header("Monsters (palette UI)")]
    [Tooltip("그리드에 배치할 몬스터 프리팹. id는 저장 JSON의 prefabId와 동일해야 Play 씬에서도 복원됨.")]
    [SerializeField] private List<MonsterPaletteEntry> monsterPalette;
    [Tooltip("에디트에서 배치한 몬스터의 부모. 비어 있으면 배치·저장이 동작하지 않음.")]
    [SerializeField] private Transform placedMonstersRoot;

    [Header("Current State")]
    [SerializeField] private TileBase paintTile;    // currently selected tile for painting
    [SerializeField] private string paintTileId;    // ID of the currently selected tile (for save/load)
    [SerializeField] private bool eraseMode;        // true = erase instead of paint

    /// <summary>선택된 파워업 팔레트 id. 비어 있지 않으면 클릭 시 해당 프리팹을 셀 중앙에 배치.</summary>
    private string selectedPowerUpId = "";

    /// <summary>선택된 물음표 블록 팔레트 id. 파워업과 동시에 선택되지 않음.</summary>
    private string selectedQuestionBlockId = "";

    /// <summary>선택된 몬스터 팔레트 id. 타일·파워업·물음표와 동시에 선택되지 않음.</summary>
    private string selectedMonsterId = "";

    private string selectedPipeEntranceId = "";
    private string selectedPipeExitId = "";

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

        // Don't paint/erase when clicking on UI. (-1 = 마우스; 인자 없는 오버로드는 환경에 따라 그리드 클릭까지 막을 수 있음)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1))
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

        if (Mouse.current.leftButton.wasReleasedThisFrame && !eraseMode && !string.IsNullOrEmpty(selectedPipeEntranceId))
        {
            if (!pointerOverSpawn)
                PlacePipeEntranceAtCell(cellPos);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && !eraseMode && !string.IsNullOrEmpty(selectedPipeExitId))
        {
            if (!pointerOverSpawn)
                PlacePipeExitAtCell(cellPos);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && !eraseMode && !string.IsNullOrEmpty(selectedQuestionBlockId))
        {
            if (!pointerOverSpawn)
                PlaceQuestionBlockAtCell(cellPos);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && !eraseMode && !string.IsNullOrEmpty(selectedMonsterId))
        {
            if (!pointerOverSpawn)
                PlaceMonsterAtCell(cellPos);
        }

        if (leftPressed)
        {
            if (eraseMode)
            {
                if (!pointerOverSpawn)
                    EraseAt(cellPos);
                ErasePowerUpsAtCell(cellPos);
                ErasePipesAtCell(cellPos);
                EraseQuestionBlocksAtCell(cellPos);
                EraseMonstersAtCell(cellPos);
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
            ErasePipesAtCell(cellPos);
            EraseQuestionBlocksAtCell(cellPos);
            EraseMonstersAtCell(cellPos);
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
        selectedPipeEntranceId = "";
        selectedPipeExitId = "";
        selectedQuestionBlockId = "";
        selectedMonsterId = "";
    }

    public void SetPaintTileById(string id)
    {
        var entry = FindEntryById(id);
        if (entry == null) return;
        paintTile = entry.tile;
        paintTileId = id;
        selectedPowerUpId = "";
        selectedPipeEntranceId = "";
        selectedPipeExitId = "";
        selectedQuestionBlockId = "";
        selectedMonsterId = "";
    }

    public void SetEraseMode(bool on)
    {
        eraseMode = on;
        if (on)
        {
            selectedPowerUpId = "";
            selectedPipeEntranceId = "";
            selectedPipeExitId = "";
            selectedQuestionBlockId = "";
            selectedMonsterId = "";
        }
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
            selectedPipeEntranceId = "";
            selectedPipeExitId = "";
            selectedQuestionBlockId = "";
            selectedMonsterId = "";
            paintTile = null;
            paintTileId = "";
            eraseMode = false;
            return;
        }
        selectedPowerUpId = "";
    }

    public void ClearPowerUpSelection() => selectedPowerUpId = "";

    /// <summary>입구 파이프 팔레트 선택. id는 PipePaletteEntry.id.</summary>
    public void SetPlacePipeEntranceById(string id)
    {
        if (string.IsNullOrEmpty(id) || pipeEntrancePalette == null)
        {
            selectedPipeEntranceId = "";
            return;
        }
        foreach (var e in pipeEntrancePalette)
        {
            if (e == null || e.prefab == null) continue;
            if (e.id != id) continue;
            selectedPipeEntranceId = id;
            selectedPipeExitId = "";
            selectedPowerUpId = "";
            selectedQuestionBlockId = "";
            selectedMonsterId = "";
            paintTile = null;
            paintTileId = "";
            eraseMode = false;
            return;
        }
        selectedPipeEntranceId = "";
    }

    /// <summary>출구 파이프 팔레트 선택. 가장 오래된 미연결 입구와 같은 pairId로 짝 지음.</summary>
    public void SetPlacePipeExitById(string id)
    {
        if (string.IsNullOrEmpty(id) || pipeExitPalette == null)
        {
            selectedPipeExitId = "";
            return;
        }
        foreach (var e in pipeExitPalette)
        {
            if (e == null || e.prefab == null) continue;
            if (e.id != id) continue;
            selectedPipeExitId = id;
            selectedPipeEntranceId = "";
            selectedPowerUpId = "";
            selectedQuestionBlockId = "";
            selectedMonsterId = "";
            paintTile = null;
            paintTileId = "";
            eraseMode = false;
            return;
        }
        selectedPipeExitId = "";
    }

    public void ClearPipeSelection()
    {
        selectedPipeEntranceId = "";
        selectedPipeExitId = "";
    }

    /// <summary>타일 팔레트에서 물음표 블록 버튼 클릭 시 호출. id는 QuestionBlockPaletteEntry.id와 동일.</summary>
    public void SetPlaceQuestionBlockById(string id)
    {
        if (string.IsNullOrEmpty(id) || questionBlockPalette == null)
        {
            selectedQuestionBlockId = "";
            return;
        }
        foreach (var e in questionBlockPalette)
        {
            if (e == null || e.prefab == null) continue;
            if (e.id != id) continue;
            selectedQuestionBlockId = id;
            selectedPowerUpId = "";
            selectedPipeEntranceId = "";
            selectedPipeExitId = "";
            selectedMonsterId = "";
            paintTile = null;
            paintTileId = "";
            eraseMode = false;
            return;
        }
        selectedQuestionBlockId = "";
    }

    public void ClearQuestionBlockSelection() => selectedQuestionBlockId = "";

    /// <summary>몬스터 팔레트에서 버튼 클릭 시 호출. id는 MonsterPaletteEntry.id와 동일.</summary>
    public void SetPlaceMonsterById(string id)
    {
        if (string.IsNullOrEmpty(id) || monsterPalette == null)
        {
            selectedMonsterId = "";
            return;
        }
        foreach (var e in monsterPalette)
        {
            if (e == null || e.prefab == null) continue;
            if (e.id != id) continue;
            selectedMonsterId = id;
            selectedPowerUpId = "";
            selectedPipeEntranceId = "";
            selectedPipeExitId = "";
            selectedQuestionBlockId = "";
            paintTile = null;
            paintTileId = "";
            eraseMode = false;
            return;
        }
        selectedMonsterId = "";
    }

    public void ClearMonsterSelection() => selectedMonsterId = "";

    public IReadOnlyList<PipePaletteEntry> GetPipeEntrancePalette() =>
        pipeEntrancePalette ?? (pipeEntrancePalette = new List<PipePaletteEntry>());

    public IReadOnlyList<PipePaletteEntry> GetPipeExitPalette() =>
        pipeExitPalette ?? (pipeExitPalette = new List<PipePaletteEntry>());

    /// <summary>TilePaletteUI에서 파워업 섹션을 그릴 때 사용.</summary>
    public IReadOnlyList<PowerUpPaletteEntry> GetPowerUpPalette() => powerUpPalette ?? (powerUpPalette = new List<PowerUpPaletteEntry>());

    /// <summary>TilePaletteUI에서 물음표 블록 섹션을 그릴 때 사용.</summary>
    public IReadOnlyList<QuestionBlockPaletteEntry> GetQuestionBlockPalette() =>
        questionBlockPalette ?? (questionBlockPalette = new List<QuestionBlockPaletteEntry>());

    /// <summary>MonsterPaletteUI에서 몬스터 버튼을 그릴 때 사용.</summary>
    public IReadOnlyList<MonsterPaletteEntry> GetMonsterPalette() =>
        monsterPalette ?? (monsterPalette = new List<MonsterPaletteEntry>());

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

    /// <summary>
    /// <see cref="MonsterPaletteRegistry"/>에서 <see cref="monsterPalette"/>를 채웁니다.
    /// </summary>
    public void LoadMonsterPalette()
    {
        if (monsterPalette == null) monsterPalette = new List<MonsterPaletteEntry>();
        if (monsterRegistryOverride != null)
            MonsterPaletteLoader.LoadFromRegistry(monsterRegistryOverride, monsterPalette);
        else
            MonsterPaletteLoader.TryLoadFromResources(monsterRegistryResourcePath, monsterPalette);
    }

    /// <summary>이전 API 호환. <see cref="LoadMonsterPalette"/>와 동일.</summary>
    public void LoadMonsterPaletteFromResources() => LoadMonsterPalette();

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

        if (data.pipeEntrances == null) data.pipeEntrances = new List<PlacedPipeEntranceData>();
        if (data.pipeExits == null) data.pipeExits = new List<PlacedPipeExitData>();
        PipePairMapUtil.ApplyFromMapData(data, pipeEntrancePalette, pipeExitPalette, placedPipeEntrancesRoot, placedPipeExitsRoot);

        if (data.questionBlocks == null) data.questionBlocks = new List<PlacedQuestionBlockData>();
        ClearPlacedQuestionBlocks();
        ApplyQuestionBlocksFromData(data.questionBlocks);

        if (data.monsters == null) data.monsters = new List<PlacedMonsterData>();
        ClearPlacedMonsters();
        if (monsterPalette == null || monsterPalette.Count == 0)
            LoadMonsterPalette();
        ApplyMonstersFromData(data.monsters);
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

        data.pipeEntrances = new List<PlacedPipeEntranceData>();
        if (placedPipeEntrancesRoot != null)
        {
            foreach (var m in placedPipeEntrancesRoot.GetComponentsInChildren<PlacedPipeEntranceEditMarker>(true))
            {
                var t = m.transform.position;
                data.pipeEntrances.Add(new PlacedPipeEntranceData
                {
                    prefabId = m.paletteId,
                    x = t.x,
                    y = t.y,
                    pairId = m.pairId
                });
            }
        }

        data.pipeExits = new List<PlacedPipeExitData>();
        if (placedPipeExitsRoot != null)
        {
            foreach (var m in placedPipeExitsRoot.GetComponentsInChildren<PlacedPipeExitEditMarker>(true))
            {
                var t = m.transform.position;
                data.pipeExits.Add(new PlacedPipeExitData
                {
                    prefabId = m.paletteId,
                    x = t.x,
                    y = t.y,
                    pairId = m.pairId
                });
            }
        }

        data.questionBlocks = new List<PlacedQuestionBlockData>();
        if (placedQuestionBlocksRoot != null)
        {
            foreach (var m in placedQuestionBlocksRoot.GetComponentsInChildren<PlacedQuestionBlockEditMarker>(true))
            {
                var t = m.transform.position;
                data.questionBlocks.Add(new PlacedQuestionBlockData
                {
                    prefabId = m.paletteId,
                    x = t.x,
                    y = t.y
                });
            }
        }

        data.monsters = new List<PlacedMonsterData>();
        if (placedMonstersRoot != null)
        {
            foreach (var m in placedMonstersRoot.GetComponentsInChildren<PlacedMonsterEditMarker>(true))
            {
                var t = m.transform.position;
                data.monsters.Add(new PlacedMonsterData
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
        selectedPipeEntranceId = "";
        selectedPipeExitId = "";
        selectedQuestionBlockId = "";
        selectedMonsterId = "";
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

    void PlacePipeEntranceAtCell(Vector3Int cell)
    {
        if (placedPipeEntrancesRoot == null) return;
        var entry = FindPipeEntranceEntryById(selectedPipeEntranceId);
        if (entry == null || entry.prefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell);
        pos.z = 0f;
        ApplyPipeGridVerticalOffset(ref pos, cell);
        var go = Instantiate(entry.prefab, pos, Quaternion.identity, placedPipeEntrancesRoot);
        var marker = go.GetComponent<PlacedPipeEntranceEditMarker>();
        if (marker == null) marker = go.AddComponent<PlacedPipeEntranceEditMarker>();
        marker.paletteId = entry.id;
        marker.pairId = Guid.NewGuid().ToString("N");
    }

    void PlacePipeExitAtCell(Vector3Int cell)
    {
        if (placedPipeExitsRoot == null) return;
        var entry = FindPipeExitEntryById(selectedPipeExitId);
        if (entry == null || entry.prefab == null) return;

        var pairEntrance = FindOldestUnpairedEntrance();
        if (pairEntrance == null)
        {
            Debug.LogWarning("[TileEditController] 출구에 연결할 입구가 없습니다. 먼저 입구 파이프를 배치하세요.");
            return;
        }

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell);
        pos.z = 0f;
        ApplyPipeGridVerticalOffset(ref pos, cell);
        var go = Instantiate(entry.prefab, pos, Quaternion.identity, placedPipeExitsRoot);
        var marker = go.GetComponent<PlacedPipeExitEditMarker>();
        if (marker == null) marker = go.AddComponent<PlacedPipeExitEditMarker>();
        marker.paletteId = entry.id;
        marker.pairId = pairEntrance.pairId;
    }

    void ApplyPipeGridVerticalOffset(ref Vector3 pos, Vector3Int cell)
    {
        if (groundTilemap == null || Mathf.Approximately(pipePlacementVerticalOffsetCells, 0f))
            return;
        float oneCellY = groundTilemap.GetCellCenterWorld(cell + Vector3Int.up).y -
                         groundTilemap.GetCellCenterWorld(cell).y;
        pos.y += oneCellY * pipePlacementVerticalOffsetCells;
    }

    /// <summary>아직 출구가 없는 입구 중, 가장 먼저 생성된 것.</summary>
    PlacedPipeEntranceEditMarker FindOldestUnpairedEntrance()
    {
        if (placedPipeEntrancesRoot == null) return null;
        var entrances = placedPipeEntrancesRoot.GetComponentsInChildren<PlacedPipeEntranceEditMarker>(true);
        if (entrances == null || entrances.Length == 0) return null;

        var exitPairIds = new HashSet<string>();
        if (placedPipeExitsRoot != null)
        {
            foreach (var ex in placedPipeExitsRoot.GetComponentsInChildren<PlacedPipeExitEditMarker>(true))
            {
                if (ex != null && !string.IsNullOrEmpty(ex.pairId))
                    exitPairIds.Add(ex.pairId);
            }
        }

        PlacedPipeEntranceEditMarker best = null;
        int bestInstanceId = int.MaxValue;
        foreach (var e in entrances)
        {
            if (e == null || string.IsNullOrEmpty(e.pairId)) continue;
            if (exitPairIds.Contains(e.pairId)) continue;
            int id = e.gameObject.GetInstanceID();
            if (id < bestInstanceId)
            {
                bestInstanceId = id;
                best = e;
            }
        }
        return best;
    }

    void ErasePipesAtCell(Vector3Int cell)
    {
        Bounds b = new Bounds(groundTilemap.GetCellCenterWorld(cell), (Vector3)groundTilemap.cellSize);
        if (placedPipeEntrancesRoot != null)
        {
            foreach (var m in placedPipeEntrancesRoot.GetComponentsInChildren<PlacedPipeEntranceEditMarker>(true))
            {
                if (m != null && b.Contains(m.transform.position))
                    Destroy(m.gameObject);
            }
        }
        if (placedPipeExitsRoot != null)
        {
            foreach (var m in placedPipeExitsRoot.GetComponentsInChildren<PlacedPipeExitEditMarker>(true))
            {
                if (m != null && b.Contains(m.transform.position))
                    Destroy(m.gameObject);
            }
        }
    }

    PipePaletteEntry FindPipeEntranceEntryById(string id)
    {
        if (string.IsNullOrEmpty(id) || pipeEntrancePalette == null) return null;
        foreach (var e in pipeEntrancePalette)
            if (e != null && e.id == id) return e;
        return null;
    }

    PipePaletteEntry FindPipeExitEntryById(string id)
    {
        if (string.IsNullOrEmpty(id) || pipeExitPalette == null) return null;
        foreach (var e in pipeExitPalette)
            if (e != null && e.id == id) return e;
        return null;
    }

    void PlaceQuestionBlockAtCell(Vector3Int cell)
    {
        if (placedQuestionBlocksRoot == null) return;
        var entry = FindQuestionBlockEntryById(selectedQuestionBlockId);
        if (entry == null || entry.prefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell);
        pos.z = 0f;
        var go = Instantiate(entry.prefab, pos, Quaternion.identity, placedQuestionBlocksRoot);
        var marker = go.GetComponent<PlacedQuestionBlockEditMarker>();
        if (marker == null) marker = go.AddComponent<PlacedQuestionBlockEditMarker>();
        marker.paletteId = entry.id;
    }

    void EraseQuestionBlocksAtCell(Vector3Int cell)
    {
        if (placedQuestionBlocksRoot == null) return;
        Bounds b = new Bounds(groundTilemap.GetCellCenterWorld(cell), (Vector3)groundTilemap.cellSize);
        var markers = placedQuestionBlocksRoot.GetComponentsInChildren<PlacedQuestionBlockEditMarker>(true);
        foreach (var m in markers)
        {
            if (m != null && b.Contains(m.transform.position))
                Destroy(m.gameObject);
        }
    }

    void ClearPlacedQuestionBlocks()
    {
        if (placedQuestionBlocksRoot == null) return;
        for (int i = placedQuestionBlocksRoot.childCount - 1; i >= 0; i--)
            Destroy(placedQuestionBlocksRoot.GetChild(i).gameObject);
    }

    void ApplyQuestionBlocksFromData(List<PlacedQuestionBlockData> list)
    {
        if (list == null || placedQuestionBlocksRoot == null) return;
        foreach (var p in list)
        {
            if (p == null || string.IsNullOrEmpty(p.prefabId)) continue;
            var entry = FindQuestionBlockEntryById(p.prefabId);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[TileEditController] Question block prefab not in palette: {p.prefabId}");
                continue;
            }
            var go = Instantiate(entry.prefab, new Vector3(p.x, p.y, 0f), Quaternion.identity, placedQuestionBlocksRoot);
            var marker = go.GetComponent<PlacedQuestionBlockEditMarker>();
            if (marker == null) marker = go.AddComponent<PlacedQuestionBlockEditMarker>();
            marker.paletteId = entry.id;
        }
    }

    QuestionBlockPaletteEntry FindQuestionBlockEntryById(string id)
    {
        if (string.IsNullOrEmpty(id) || questionBlockPalette == null) return null;
        foreach (var e in questionBlockPalette)
            if (e != null && e.id == id) return e;
        return null;
    }

    void PlaceMonsterAtCell(Vector3Int cell)
    {
        if (placedMonstersRoot == null)
        {
            Debug.LogWarning("[TileEditController] placedMonstersRoot가 없습니다. 에디트 씬에서 TileEditController의 Placed Monsters Root에 부모 Transform을 지정하세요.");
            return;
        }

        if (monsterPalette == null || monsterPalette.Count == 0)
            LoadMonsterPalette();

        var entry = FindMonsterEntryById(selectedMonsterId);
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"[TileEditController] 몬스터 id '{selectedMonsterId}'에 해당하는 프리팹이 없습니다. MonsterPaletteRegistry의 entries와 id를 확인하세요.");
            return;
        }

        // 타일이 칸을 채울 때 “서 있는 면”은 셀의 위 변. 아래 변을 쓰면 발이 블록 한 칸 아래(칸 안쪽)로 맞춰짐.
        Vector3 surfaceCenter = GetCellTopCenterWorld(cell);
        var go = Instantiate(entry.prefab, surfaceCenter, Quaternion.identity, placedMonstersRoot);
        AlignMonsterBottomToWorldY(go, surfaceCenter.y);
        var marker = go.GetComponent<PlacedMonsterEditMarker>();
        if (marker == null) marker = go.AddComponent<PlacedMonsterEditMarker>();
        marker.paletteId = entry.id;
    }

    /// <summary>셀의 위 변 중앙(월드). 그리드 칸에 깔린 블록의 “윗면”에 발을 맞출 때 사용.</summary>
    Vector3 GetCellTopCenterWorld(Vector3Int cell)
    {
        Vector3 center = groundTilemap.GetCellCenterWorld(cell);
        float halfY = groundTilemap.cellSize.y * Mathf.Abs(groundTilemap.transform.lossyScale.y) * 0.5f;
        center.y += halfY;
        center.z = 0f;
        return center;
    }

    /// <summary>인스턴스의 시각·물리 하단(콜라이더 우선, 없으면 SpriteRenderer)이 <paramref name="worldFloorY"/>에 오도록 Y만 이동.</summary>
    static void AlignMonsterBottomToWorldY(GameObject instance, float worldFloorY)
    {
        float minY = float.MaxValue;
        foreach (var col in instance.GetComponentsInChildren<Collider2D>(true))
        {
            if (!col.enabled) continue;
            minY = Mathf.Min(minY, col.bounds.min.y);
        }
        if (minY < float.MaxValue)
        {
            instance.transform.position += new Vector3(0f, worldFloorY - minY, 0f);
            return;
        }
        foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || !sr.enabled) continue;
            minY = Mathf.Min(minY, sr.bounds.min.y);
        }
        if (minY < float.MaxValue)
            instance.transform.position += new Vector3(0f, worldFloorY - minY, 0f);
    }

    void EraseMonstersAtCell(Vector3Int cell)
    {
        if (placedMonstersRoot == null) return;
        Bounds b = new Bounds(groundTilemap.GetCellCenterWorld(cell), (Vector3)groundTilemap.cellSize);
        var markers = placedMonstersRoot.GetComponentsInChildren<PlacedMonsterEditMarker>(true);
        foreach (var m in markers)
        {
            if (m != null && b.Contains(m.transform.position))
                Destroy(m.gameObject);
        }
    }

    void ClearPlacedMonsters()
    {
        if (placedMonstersRoot == null) return;
        for (int i = placedMonstersRoot.childCount - 1; i >= 0; i--)
            Destroy(placedMonstersRoot.GetChild(i).gameObject);
    }

    void ApplyMonstersFromData(List<PlacedMonsterData> list)
    {
        if (list == null || placedMonstersRoot == null) return;
        foreach (var p in list)
        {
            if (p == null || string.IsNullOrEmpty(p.prefabId)) continue;
            var entry = FindMonsterEntryById(p.prefabId);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[TileEditController] Monster prefab not in palette: {p.prefabId}");
                continue;
            }
            var go = Instantiate(entry.prefab, new Vector3(p.x, p.y, 0f), Quaternion.identity, placedMonstersRoot);
            var marker = go.GetComponent<PlacedMonsterEditMarker>();
            if (marker == null) marker = go.AddComponent<PlacedMonsterEditMarker>();
            marker.paletteId = entry.id;
        }
    }

    MonsterPaletteEntry FindMonsterEntryById(string id)
    {
        if (string.IsNullOrEmpty(id) || monsterPalette == null) return null;
        foreach (var e in monsterPalette)
            if (e != null && e.id == id) return e;
        return null;
    }
}