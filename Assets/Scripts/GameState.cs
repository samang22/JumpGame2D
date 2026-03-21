using System.Collections.Generic;

/// <summary>
/// Cross-scene state: test play vs real play, and map data / selected map ID.
/// No MonoBehaviour — use static members only. Survives scene load.
/// </summary>
public static class GameState
{
    /// <summary>True when entering Play from Edit (Test Play); false when from Map List.</summary>
    public static bool IsTestPlay;

    /// <summary>When IsTestPlay is false, the map ID to load from file (e.g. file name).</summary>
    public static string SelectedMapId;

    /// <summary>When IsTestPlay is true, the in-memory map data passed from Edit. Used by Play scene to restore tilemaps.</summary>
    public static MapData TestPlayMapData;

    /// <summary>Edit 씬에서 타일 편집 중일 때 true(Test Play 아님). 파워업 등은 이때 움직이지 않음.</summary>
    public static bool IsMapEditMode;
}

/// <summary>
/// Serializable map data: which tile ID is at which cell, per layer.
/// Used for test-play handoff and for save/load.
/// </summary>
[System.Serializable]
public class MapData
{
    /// <summary>Player spawn position (world X). Used when loading map or test play.</summary>
    public float spawnX;
    /// <summary>Player spawn position (world Y).</summary>
    public float spawnY;

    /// <summary>Goal marker position (world X).</summary>
    public float goalX;
    /// <summary>Goal marker position (world Y).</summary>
    public float goalY;

    public List<TileCellData> groundCells = new List<TileCellData>();
    public List<TileCellData> oneWayCells = new List<TileCellData>();
    public List<TileCellData> backgroundCells = new List<TileCellData>();
    public List<TileCellData> gimmickCells = new List<TileCellData>();
    public List<TileCellData> hazardCells = new List<TileCellData>();

    /// <summary>에디트에서 배치한 파워업(버섯/꽃 등). prefabId는 PowerUpPaletteEntry.id와 일치해야 함.</summary>
    public List<PlacedPowerUpData> powerUps = new List<PlacedPowerUpData>();
}

[System.Serializable]
public class PlacedPowerUpData
{
    public string prefabId;
    public float x;
    public float y;
}

[System.Serializable]
public class TileCellData
{
    public int x;
    public int y;
    public string tileId;
}
