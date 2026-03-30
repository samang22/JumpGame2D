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

    /// <summary>Test Play 직전 Edit 씬에서 스냅샷한 맵 데이터. Return to Edit 시 타일·몬스터·파워업 등 복원에 사용.</summary>
    public static MapData TestPlayMapData;

    /// <summary>Edit 씬에서 타일 편집 중일 때 true(Test Play 아님). 파워업 등은 이때 움직이지 않음.</summary>
    public static bool IsMapEditMode;

    /// <summary>클리어 후 플레이어 Victory 애니 재생 중일 때 true. 몬스터 이동/AI 정지용.</summary>
    public static bool IsVictory;
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

    /// <summary>에디트에서 배치한 물음표 블록. prefabId는 QuestionBlockPaletteEntry.id와 일치해야 함.</summary>
    public List<PlacedQuestionBlockData> questionBlocks = new List<PlacedQuestionBlockData>();

    /// <summary>에디트에서 배치한 몬스터. prefabId는 MonsterPaletteEntry.id와 일치해야 함.</summary>
    public List<PlacedMonsterData> monsters = new List<PlacedMonsterData>();

    /// <summary>입구 파이프. pairId로 pipeExits와 짝을 맞춤.</summary>
    public List<PlacedPipeEntranceData> pipeEntrances = new List<PlacedPipeEntranceData>();

    /// <summary>출구 파이프. pairId로 pipeEntrances와 짝을 맞춤.</summary>
    public List<PlacedPipeExitData> pipeExits = new List<PlacedPipeExitData>();
}

[System.Serializable]
public class PlacedPowerUpData
{
    public string prefabId;
    public float x;
    public float y;
}

[System.Serializable]
public class PlacedQuestionBlockData
{
    public string prefabId;
    public float x;
    public float y;
}

[System.Serializable]
public class PlacedMonsterData
{
    public string prefabId;
    public float x;
    public float y;
}

[System.Serializable]
public class PlacedPipeEntranceData
{
    public string prefabId;
    public float x;
    public float y;
    public string pairId;
}

[System.Serializable]
public class PlacedPipeExitData
{
    public string prefabId;
    public float x;
    public float y;
    public string pairId;
}

[System.Serializable]
public class TileCellData
{
    public int x;
    public int y;
    public string tileId;
}
