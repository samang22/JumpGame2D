using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using TMPro;

public class EditSceneController : MonoBehaviour
{
    [Header("Edit / Test Play (same scene)")]
    [SerializeField] private TileEditController tileEditController;
    [Tooltip("Canvas or panel that contains editor UI (palette, Test Play, Save, etc.). Hidden during test play.")]
    [SerializeField] private GameObject editorUIRoot;
    [Tooltip("Player GameObject. Test Play 시에만 활성화됨 (스폰 마커 사용 시).")]
    [SerializeField] private GameObject player;
    [Tooltip("(선택) 스폰 마커. 넣으면 에디트에서는 이만 보이고 드래그, Test Play 시 플레이어를 이 위치에 두고 플레이어 활성화.")]
    [SerializeField] private GameObject spawnMarker;
    [Tooltip("스폰 마커를 쓰지 않을 때만 사용. 에디트에서 비활성화할 플레이어 컴포넌트 (이동/점프 등).")]
    [SerializeField] private Behaviour[] playerBehavioursToDisableInEditMode;
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
    [Tooltip("저장 성공 시 잠깐 표시할 텍스트 오브젝트 (TMP_Text). 없으면 무시.")]
    [SerializeField] private TMP_Text saveFeedbackText;
    [SerializeField] private float saveFeedbackDuration = 2f;

    private void Start()
    {
        EnterEditMode();
        SetTestPlayButtonLabel("Test Play");
        TryLoadSelectedMap();
    }

    private void TryLoadSelectedMap()
    {
        if (string.IsNullOrEmpty(GameState.SelectedMapId)) return;
        if (tileEditController == null) return;

        string dir = System.IO.Path.Combine(Application.persistentDataPath, saveSubFolder);
        string path = System.IO.Path.Combine(dir, GameState.SelectedMapId + ".json");

        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[EditSceneController] Map file not found: {path}");
            return;
        }

        string json = System.IO.File.ReadAllText(path);
        MapData data = JsonUtility.FromJson<MapData>(json);
        tileEditController.ApplyMapData(data);
        Debug.Log($"[EditSceneController] Map loaded: {GameState.SelectedMapId}");
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
        if (spawnMarker != null)
        {
            if (player != null)
            {
                var pos = player.transform.position;
                pos.x = spawnMarker.transform.position.x;
                pos.y = spawnMarker.transform.position.y;
                player.transform.position = pos;
                player.SetActive(true);
            }
            spawnMarker.SetActive(false);
        }
        else
        {
            if (player != null) player.SetActive(true);
            SetPlayerBehavioursEnabled(true);
        }
        if (cameraFollow != null) cameraFollow.enabled = true;
        if (backToEditUIRoot != null) backToEditUIRoot.SetActive(true);

        SetTestPlayButtonLabel("Return to Edit");
    }

    /// <summary>Called from Back to Edit button OnClick. Returns to edit mode in the same scene.</summary>
    public void OnBackToEditClicked()
    {
        GameState.IsTestPlay = false;

        if (tileEditController != null) tileEditController.enabled = true;
        if (spawnMarker != null)
        {
            if (player != null) player.SetActive(false);
            spawnMarker.SetActive(true);
        }
        else
        {
            if (player != null) player.SetActive(true);
            SetPlayerBehavioursEnabled(false);
        }
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
        if (spawnMarker != null)
        {
            if (player != null) player.SetActive(false);
            spawnMarker.SetActive(true);
        }
        else
        {
            if (player != null) player.SetActive(true);
            SetPlayerBehavioursEnabled(false);
        }
        if (cameraFollow != null) cameraFollow.enabled = false;
        if (backToEditUIRoot != null) backToEditUIRoot.SetActive(false);
        if (editorUIRoot != null) editorUIRoot.SetActive(true);
        if (tileEditController != null) tileEditController.enabled = true;
        SetTestPlayButtonLabel("Test Play");
    }

    private void SetPlayerBehavioursEnabled(bool enabled)
    {
        if (playerBehavioursToDisableInEditMode == null) return;
        foreach (var b in playerBehavioursToDisableInEditMode)
        {
            if (b != null) b.enabled = enabled;
        }
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
        ShowSaveFeedback($"Saved: {mapId}");
    }

    private void ShowSaveFeedback(string message)
    {
        if (saveFeedbackText == null) return;
        StopCoroutine(nameof(HideFeedbackAfterDelay));
        saveFeedbackText.text = message;
        saveFeedbackText.gameObject.SetActive(true);
        StartCoroutine(nameof(HideFeedbackAfterDelay));
    }

    private IEnumerator HideFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(saveFeedbackDuration);
        if (saveFeedbackText != null)
            saveFeedbackText.gameObject.SetActive(false);
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
