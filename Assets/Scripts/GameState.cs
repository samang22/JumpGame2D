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
}

[System.Serializable]
public class TileCellData
{
    public int x;
    public int y;
    public string tileId;
}
