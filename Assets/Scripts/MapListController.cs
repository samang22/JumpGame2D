using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class MapListController : MonoBehaviour
{
    [Header("List UI")]
    [SerializeField] private Transform listContent;
    [SerializeField] private GameObject mapButtonPrefab;

    [Header("Mode Popup")]
    [SerializeField] private GameObject modePopup;
    [SerializeField] private TMP_Text popupTitleText;

    [Header("Save Settings")]
    [SerializeField] private string saveSubFolder = "Maps";

    private string selectedMapId;

    private void Start()
    {
        if (modePopup != null)
            modePopup.SetActive(false);

        PopulateList();
    }

    private void PopulateList()
    {
        if (listContent == null || mapButtonPrefab == null)
        {
            Debug.LogWarning("[MapListController] listContent or mapButtonPrefab is not assigned.");
            return;
        }

        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        string dir = Path.Combine(Application.persistentDataPath, saveSubFolder);
        if (!Directory.Exists(dir))
        {
            Debug.Log($"[MapListController] Save folder not found: {dir}");
            return;
        }

        string[] files = Directory.GetFiles(dir, "*.json");
        foreach (string path in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);

            GameObject go = Instantiate(mapButtonPrefab, listContent);

            // 맵 이름 표시 (Button_Map 자식의 TMP_Text)
            var mapBtn = go.transform.Find("Button_Map")?.GetComponent<Button>()
                         ?? go.GetComponent<Button>();
            var label = mapBtn != null
                ? mapBtn.GetComponentInChildren<TMP_Text>()
                : go.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = fileName;

            string mapId = fileName;
            if (mapBtn != null)
                mapBtn.onClick.AddListener(() => OnMapButtonClicked(mapId));

            // 삭제 버튼 (Button_Delete)
            var deleteBtn = go.transform.Find("Button_Delete")?.GetComponent<Button>();
            if (deleteBtn != null)
                deleteBtn.onClick.AddListener(() => OnDeleteButtonClicked(mapId));
        }
    }

    private void OnMapButtonClicked(string mapId)
    {
        selectedMapId = mapId;
        GameState.SelectedMapId = mapId;
        GameState.IsTestPlay = false;

        if (popupTitleText != null)
            popupTitleText.text = $"How would you like to open \"{mapId}\"?";

        if (modePopup != null)
            modePopup.SetActive(true);
    }

    /// <summary>Button_Edit�� OnClick���� ȣ��. sceneName�� "Edit" �� �� �̸��� ���� �Է�.</summary>
    public void OnClickEdit(string sceneName)
    {
        if (string.IsNullOrEmpty(selectedMapId))
        {
            Debug.LogWarning("[MapListController] No map selected (Edit).");
            return;
        }
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[MapListController] Scene name is empty (Edit).");
            return;
        }

        GameState.IsTestPlay = false;
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Button_Play�� OnClick���� ȣ��. sceneName�� "Play" �� �� �̸��� ���� �Է�.</summary>
    public void OnClickPlay(string sceneName)
    {
        if (string.IsNullOrEmpty(selectedMapId))
        {
            Debug.LogWarning("[MapListController] No map selected (Play).");
            return;
        }
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[MapListController] Scene name is empty (Play).");
            return;
        }

        GameState.IsTestPlay = false;
        SceneManager.LoadScene(sceneName);
    }

    private void OnDeleteButtonClicked(string mapId)
    {
        ExecuteDelete(mapId);
    }

    private void ExecuteDelete(string mapId)
    {
        string dir = Path.Combine(Application.persistentDataPath, saveSubFolder);
        string filePath = Path.Combine(dir, mapId + ".json");

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"[MapListController] Deleted: {filePath}");
        }
        else
        {
            Debug.LogWarning($"[MapListController] File not found for deletion: {filePath}");
        }

        PopulateList();
    }

    public void OnClickCancel()
    {
        selectedMapId = null;
        GameState.SelectedMapId = null;
        if (modePopup != null)
            modePopup.SetActive(false);
    }

    /// <summary>�� �� ����� ��ư. sceneName�� ������ �� �̸��� ���� �Է�.</summary>
    public void OnNewMapClicked(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[MapListController] Scene name is empty (NewMap).");
            return;
        }

        selectedMapId = null;
        GameState.IsTestPlay = false;
        GameState.SelectedMapId = null;
        SceneManager.LoadScene(sceneName);
    }
}
