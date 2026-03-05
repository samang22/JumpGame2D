using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class EditSceneController : MonoBehaviour
{
    [SerializeField] private TileEditController tileEditController;
    [Tooltip("Scene name to load when Test Play is pressed.")]
    [SerializeField] private string playSceneName = "Play";
    [Tooltip("Scene name to load when To Map List is pressed.")]
    [SerializeField] private string mapListSceneName = "MapList";
    [Tooltip("Subfolder under persistentDataPath for saved maps. File name = mapId + .json")]
    [SerializeField] private string saveSubFolder = "Maps";

    /// <summary>Called from TestPlay button OnClick.</summary>
    public void OnTestPlayClicked()
    {
        if (tileEditController == null) return;

        MapData data = tileEditController.CollectMapData();
        GameState.TestPlayMapData = data;
        GameState.IsTestPlay = true;
        GameState.SelectedMapId = null;

        if (!string.IsNullOrEmpty(playSceneName))
            SceneManager.LoadScene(playSceneName);
    }

    /// <summary>Called from To Map List button OnClick.</summary>
    public void OnToMapListClicked()
    {
        if (!string.IsNullOrEmpty(mapListSceneName))
            SceneManager.LoadScene(mapListSceneName);
    }

    /// <summary>Called from Save button OnClick. Saves current map as JSON. Uses mapId for file name; set before calling or pass via UI.</summary>
    public void OnSaveClicked(string mapId)
    {
        if (tileEditController == null || string.IsNullOrEmpty(mapId)) return;

        MapData data = tileEditController.CollectMapData();
        string dir = Path.Combine(Application.persistentDataPath, saveSubFolder);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, mapId + ".json");
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(path, json);
        Debug.Log($"Map saved: {path}");
    }

    /// <summary>Overload for Save button when mapId is fixed or from a single input field elsewhere. Uses default name if empty.</summary>
    public void OnSaveClicked()
    {
        OnSaveClicked("map");
    }
}
