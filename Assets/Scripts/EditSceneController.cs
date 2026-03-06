using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;
using TMPro;

public class EditSceneController : MonoBehaviour
{
    [Header("Edit / Test Play (same scene)")]
    [SerializeField] private TileEditController tileEditController;
    [Tooltip("Canvas or panel that contains editor UI (palette, Test Play, Save, etc.). Hidden during test play.")]
    [SerializeField] private GameObject editorUIRoot;
    [Tooltip("Player GameObject. Disabled in edit, enabled when test play.")]
    [SerializeField] private GameObject player;
    [Tooltip("Camera follow component on Main Camera. Disabled in edit, enabled when test play so same camera follows player.")]
    [SerializeField] private CameraFollow cameraFollow;
    [Tooltip("Panel or button 'Back to Edit'. Shown only during test play.")]
    [SerializeField] private GameObject backToEditUIRoot;
    [Tooltip("Text of the Test Play button. Will be set to 'Return to Edit' during test play.")]
    [SerializeField] private TMP_Text testPlayButtonLabel;
    [Tooltip("If not using TMP, assign the legacy Text component of the Test Play button.")]
    [SerializeField] private Text testPlayButtonLabelLegacy;

    [Header("Navigation")]
    [Tooltip("Scene name to load when To Map List is pressed.")]
    [SerializeField] private string mapListSceneName = "MapList";
    [Tooltip("Subfolder under persistentDataPath for saved maps. File name = mapId + .json")]
    [SerializeField] private string saveSubFolder = "Maps";

    [SerializeField] private TMP_InputField mapNameInput;

    private void Start()
    {
        EnterEditMode();
        SetTestPlayButtonLabel("Test Play");
    }

    /// <summary>Call this from the Test Play button OnClick. Toggles between edit and test play; button text switches between "Test Play" and "Return to Edit".</summary>
    public void OnTestPlayToggleClicked()
    {
        if (GameState.IsTestPlay)
            OnBackToEditClicked();
        else
            OnTestPlayClicked();
    }

    /// <summary>Called from TestPlay button OnClick. Stays in Edit scene; only controller and camera behavior switch.</summary>
    public void OnTestPlayClicked()
    {
        GameState.IsTestPlay = true;
        GameState.SelectedMapId = null;

        if (tileEditController != null) tileEditController.enabled = false;
        // if (editorUIRoot != null) editorUIRoot.SetActive(false); // keep visible so same button can show "Return to Edit"
        if (player != null) player.SetActive(true);
        if (cameraFollow != null) cameraFollow.enabled = true;
        if (backToEditUIRoot != null) backToEditUIRoot.SetActive(true);

        SetTestPlayButtonLabel("Return to Edit");
    }

    /// <summary>Called from Back to Edit button OnClick. Returns to edit mode in the same scene.</summary>
    public void OnBackToEditClicked()
    {
        GameState.IsTestPlay = false;

        if (tileEditController != null) tileEditController.enabled = true;
        // if (editorUIRoot != null) editorUIRoot.SetActive(true);
        if (player != null) player.SetActive(false);
        if (cameraFollow != null) cameraFollow.enabled = false;
        if (backToEditUIRoot != null) backToEditUIRoot.SetActive(false);

        SetTestPlayButtonLabel("Test Play");
    }

    private void SetTestPlayButtonLabel(string text)
    {
        if (testPlayButtonLabel != null) testPlayButtonLabel.text = text;
        if (testPlayButtonLabelLegacy != null) testPlayButtonLabelLegacy.text = text;
    }

    private void EnterEditMode()
    {
        if (player != null) player.SetActive(false);
        if (cameraFollow != null) cameraFollow.enabled = false;
        if (backToEditUIRoot != null) backToEditUIRoot.SetActive(false);
        if (editorUIRoot != null) editorUIRoot.SetActive(true);
        if (tileEditController != null) tileEditController.enabled = true;
        SetTestPlayButtonLabel("Test Play");
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

    public void OnSaveClickedFromUI()
    {
        if (mapNameInput == null)
        {
            OnSaveClicked("map"); // Ȥ�� �ƹ��͵� �� �� �� �⺻��
            return;
        }
        string id = mapNameInput.text;
        if (string.IsNullOrWhiteSpace(id))
            id = "map";          // ��� ������ �⺻ �̸�
        OnSaveClicked(id);       // ���� OnSaveClicked(string mapId) ����
    }

    /// <summary>Overload for Save button when mapId is fixed or from a single input field elsewhere. Uses default name if empty.</summary>
    public void OnSaveClicked()
    {
        OnSaveClicked("map");
    }
}
