using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using TMPro;
using System.Linq;

/// <summary>
/// TileEditController의 팔레트를 읽어 버튼을 자동 생성하고,
/// 각 버튼 아이콘은 타일에서 자동 추출(GetDisplaySprite)하여 세팅합니다.
/// </summary>
public class TilePaletteUI : MonoBehaviour
{
    [SerializeField] private TileEditController tileEditController;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab;
    [Tooltip("섹션 그룹 프리팹. 자식에 헤더용 Text + 이름이 GridRoot인 오브젝트(Grid Layout Group 붙임) 있으면 타일을 그리드로 여러 줄 표시.")]
    [SerializeField] private GameObject sectionGroupPrefab;
    [Tooltip("섹션 그룹 프리팹 안에서 타일 버튼을 넣을 자식 이름. 기본 GridRoot.")]
    [SerializeField] private string gridRootChildName = "GridRoot";
    [SerializeField] private GameObject clearButtonPrefab;
    private void Start()
    {
        //if (tileEditController != null)
        //    tileEditController.LoadPaletteFromResources();
        //RefreshPalette();
    }

    /// <summary>Shows or hides the tile palette panel. Call from "Tile List" button OnClick. 패널을 열 때마다 Resources에서 팔레트를 다시 불러와 섹션 구분을 적용합니다.</summary>
    public void TogglePalettePanel()
    {
        bool willShow = !gameObject.activeSelf;
        Debug.Log($"[TilePaletteUI] TogglePalettePanel called. activeSelf={gameObject.activeSelf}, willShow={willShow}, pos={((RectTransform)transform).anchoredPosition}, size={((RectTransform)transform).sizeDelta}");

        if (willShow)
        {
            // 먼저 패널을 켜고
            gameObject.SetActive(true);

            // 그 상태에서 팔레트 로드 + UI 리프레시
            if (tileEditController != null)
            {
                tileEditController.LoadPaletteFromResources();
                RefreshPalette();
            }
        }
        else
        {
            // 닫을 때는 단순히 비활성화만
            gameObject.SetActive(false);
        }
    }

    /// <summary>팔레트 버튼을 다시 그립니다. 레이어별로 섹션 그룹 + 버튼 생성.</summary>
    public void RefreshPalette()
    {
        if (tileEditController == null || buttonContainer == null || buttonPrefab == null)
            return;

        for (int i = buttonContainer.childCount - 1; i >= 0; i--)
            Destroy(buttonContainer.GetChild(i).gameObject);

        IReadOnlyList<TilePaletteEntry> palette = tileEditController.GetPalette();
        if (palette == null || palette.Count == 0)
            return;

        bool useSectionGroup = sectionGroupPrefab != null;
        var layerOrder = new[] { TileLayerType.Solid, TileLayerType.OneWay, TileLayerType.BackGround, TileLayerType.Gimmick, TileLayerType.Hazard };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if ((useSectionGroup) && palette.Count > 0)
        {
            var sb = new System.Text.StringBuilder("Palette 섹션: ");
            foreach (var layer in layerOrder)
            {
                int n = 0;
                foreach (var e in palette) if (e.tile != null && e.layer == layer) n++;
                if (n > 0) sb.Append(TileLayerTypeDisplay.GetDisplayName(layer)).Append("=").Append(n).Append(" ");
            }
            Debug.Log(sb.ToString());
        }
#endif

        if (clearButtonPrefab != null)
        {
            var clear = Instantiate(clearButtonPrefab, buttonContainer);
            var btn = clear.GetComponentInChildren<Button>();
            if (btn != null && tileEditController != null)
                btn.onClick.AddListener(() => tileEditController.ClearPaintSelection());
        }

        // 파워업(버섯/꽃 등) — 타일과 동일 패널에서 선택
        var powerUpList = tileEditController != null ? tileEditController.GetPowerUpPalette() : null;
        if (powerUpList != null && powerUpList.Count > 0)
        {
            if (useSectionGroup && sectionGroupPrefab != null)
            {
                var puGroup = Instantiate(sectionGroupPrefab, buttonContainer);
                var tmpPu = puGroup.GetComponentInChildren<TMP_Text>();
                if (tmpPu != null) tmpPu.text = "Items";
                else
                {
                    var legPu = puGroup.GetComponentInChildren<Text>();
                    if (legPu != null) legPu.text = "Items";
                }
                Transform puGrid = puGroup.transform.Find(gridRootChildName);
                if (puGrid == null) puGrid = puGroup.transform;
                foreach (var e in powerUpList)
                {
                    if (e == null || e.prefab == null) continue;
                    AddPowerUpButton(e, puGrid);
                }
            }
            else
            {
                foreach (var e in powerUpList)
                {
                    if (e == null || e.prefab == null) continue;
                    AddPowerUpButton(e, buttonContainer);
                }
            }
        }

        var questionBlockList = tileEditController != null ? tileEditController.GetQuestionBlockPalette() : null;
        if (questionBlockList != null && questionBlockList.Count > 0)
        {
            if (useSectionGroup && sectionGroupPrefab != null)
            {
                var qbGroup = Instantiate(sectionGroupPrefab, buttonContainer);
                var tmpQb = qbGroup.GetComponentInChildren<TMP_Text>();
                if (tmpQb != null) tmpQb.text = "Question blocks";
                else
                {
                    var legQb = qbGroup.GetComponentInChildren<Text>();
                    if (legQb != null) legQb.text = "Question blocks";
                }
                Transform qbGrid = qbGroup.transform.Find(gridRootChildName);
                if (qbGrid == null) qbGrid = qbGroup.transform;
                foreach (var e in questionBlockList)
                {
                    if (e == null || e.prefab == null) continue;
                    AddQuestionBlockButton(e, qbGrid);
                }
            }
            else
            {
                foreach (var e in questionBlockList)
                {
                    if (e == null || e.prefab == null) continue;
                    AddQuestionBlockButton(e, buttonContainer);
                }
            }
        }

        if (useSectionGroup)
        {
            foreach (var layer in layerOrder)
            {
                var entriesOfLayer = palette.Where(e => e.tile != null && e.layer == layer).ToList();
                if (entriesOfLayer.Count == 0) continue;

                var group = Instantiate(sectionGroupPrefab, buttonContainer);
                string headerText = TileLayerTypeDisplay.GetDisplayName(layer);
                var tmp = group.GetComponentInChildren<TMP_Text>();
                if (tmp != null) tmp.text = headerText;
                else
                {
                    var leg = group.GetComponentInChildren<Text>();
                    if (leg != null) leg.text = headerText;
                }

                Transform gridRoot = group.transform.Find(gridRootChildName);
                if (gridRoot == null) gridRoot = group.transform;

                foreach (var entry in entriesOfLayer)
                    AddButton(entry, gridRoot);
            }
        }
        else
        {
            foreach (TilePaletteEntry entry in palette)
            {
                if (entry.tile == null) continue;
                AddButton(entry, buttonContainer);
            }
        }

        // 레이아웃 갱신 (한 프레임 뒤에 다시 해서 Scroll View Content가 확실히 갱신되도록)
        var containerRect = buttonContainer as RectTransform;
        if (containerRect != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }


    private void AddButton(TilePaletteEntry entry, Transform parent)
    {
        GameObject go = Instantiate(buttonPrefab, parent);
        Button btn = go.GetComponent<Button>();
        if (btn == null) btn = go.GetComponentInChildren<Button>();

        Image img = go.GetComponentInChildren<Image>();
        if (img != null)
        {
            Sprite sprite = entry.GetDisplaySprite();
            if (sprite != null)
                img.sprite = sprite;
        }

        string label = string.IsNullOrEmpty(entry.displayName) ? entry.id : entry.displayName;
        var tmpText = go.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
            tmpText.text = label;
        else
        {
            var legacyText = go.GetComponentInChildren<Text>();
            if (legacyText != null) legacyText.text = label;
        }

        TileBase tileToSet = entry.tile;
        if (btn != null)
            btn.onClick.AddListener(() => tileEditController.SetPaintTile(tileToSet));
    }

    private void AddPowerUpButton(PowerUpPaletteEntry entry, Transform parent)
    {
        GameObject go = Instantiate(buttonPrefab, parent);
        Button btn = go.GetComponent<Button>();
        if (btn == null) btn = go.GetComponentInChildren<Button>();

        Image img = go.GetComponentInChildren<Image>();
        if (img != null)
        {
            Sprite sprite = entry.GetDisplaySprite();
            if (sprite != null)
                img.sprite = sprite;
        }

        string label = entry.GetLabel();
        var tmpText = go.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
            tmpText.text = label;
        else
        {
            var legacyText = go.GetComponentInChildren<Text>();
            if (legacyText != null) legacyText.text = label;
        }

        string id = entry.id;
        if (btn != null && tileEditController != null)
            btn.onClick.AddListener(() => tileEditController.SetPlacePowerUpById(id));
    }

    private void AddQuestionBlockButton(QuestionBlockPaletteEntry entry, Transform parent)
    {
        GameObject go = Instantiate(buttonPrefab, parent);
        Button btn = go.GetComponent<Button>();
        if (btn == null) btn = go.GetComponentInChildren<Button>();

        Image img = go.GetComponentInChildren<Image>();
        if (img != null)
        {
            Sprite sprite = entry.GetDisplaySprite();
            if (sprite != null)
                img.sprite = sprite;
        }

        string label = entry.GetLabel();
        var tmpText = go.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
            tmpText.text = label;
        else
        {
            var legacyText = go.GetComponentInChildren<Text>();
            if (legacyText != null) legacyText.text = label;
        }

        string id = entry.id;
        if (btn != null && tileEditController != null)
            btn.onClick.AddListener(() => tileEditController.SetPlaceQuestionBlockById(id));
    }
}
