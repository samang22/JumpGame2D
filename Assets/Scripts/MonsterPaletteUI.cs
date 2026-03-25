using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 타일 팔레트와 별도 패널에서 몬스터만 선택합니다.
/// UI 버튼 OnClick → <see cref="ToggleMonsterPalettePanel"/> 연결.
/// </summary>
public class MonsterPaletteUI : MonoBehaviour
{
    [SerializeField] private TileEditController tileEditController;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab;
    [Tooltip("섹션 헤더 + GridRoot가 있는 그룹 프리팹. TilePaletteUI와 동일 구조면 됨.")]
    [SerializeField] private GameObject sectionGroupPrefab;
    [SerializeField] private string gridRootChildName = "GridRoot";
    [SerializeField] private GameObject clearSelectionButtonPrefab;

    /// <summary>몬스터 팔레트 패널을 켜거나 끕니다. "Monster List" 등 버튼에 연결.</summary>
    public void ToggleMonsterPalettePanel()
    {
        bool willShow = !gameObject.activeSelf;
        if (willShow)
        {
            gameObject.SetActive(true);
            if (tileEditController != null)
                tileEditController.LoadMonsterPalette();
            RefreshMonsterPalette();
        }
        else
            gameObject.SetActive(false);
    }

    /// <summary>팔레트 버튼을 다시 그립니다.</summary>
    public void RefreshMonsterPalette()
    {
        if (tileEditController == null || buttonContainer == null || buttonPrefab == null)
            return;

        for (int i = buttonContainer.childCount - 1; i >= 0; i--)
            Destroy(buttonContainer.GetChild(i).gameObject);

        var list = tileEditController.GetMonsterPalette();
        if (list == null || list.Count == 0)
            return;

        if (clearSelectionButtonPrefab != null)
        {
            var clear = Instantiate(clearSelectionButtonPrefab, buttonContainer);
            var btn = clear.GetComponentInChildren<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => tileEditController.ClearMonsterSelection());
        }

        bool useSection = sectionGroupPrefab != null;
        Transform gridRoot = buttonContainer;

        if (useSection)
        {
            var group = Instantiate(sectionGroupPrefab, buttonContainer);
            var tmp = group.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = "Monsters";
            else
            {
                var leg = group.GetComponentInChildren<Text>();
                if (leg != null) leg.text = "Monsters";
            }
            gridRoot = group.transform.Find(gridRootChildName);
            if (gridRoot == null) gridRoot = group.transform;
        }

        foreach (var e in list)
        {
            if (e == null || e.prefab == null) continue;
            AddMonsterButton(e, gridRoot);
        }

        var containerRect = buttonContainer as RectTransform;
        if (containerRect != null)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
    }

    private void AddMonsterButton(MonsterPaletteEntry entry, Transform parent)
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
            btn.onClick.AddListener(() => tileEditController.SetPlaceMonsterById(id));
    }
}
