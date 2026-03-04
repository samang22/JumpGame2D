using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// TileEditController의 팔레트를 읽어 버튼을 자동 생성하고,
/// 각 버튼 아이콘은 타일에서 자동 추출(GetDisplaySprite)하여 세팅합니다.
/// </summary>
public class TilePaletteUI : MonoBehaviour
{
    [SerializeField] private TileEditController tileEditController;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab;

    private void Start()
    {
        RefreshPalette();
    }

    /// <summary>Shows or hides the tile palette panel. Call from "Tile List" button OnClick.</summary>
    public void TogglePalettePanel()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }

    /// <summary>팔레트 버튼을 다시 그립니다. LoadPaletteFromResources() 호출 후 자동 호출됨.</summary>
    public void RefreshPalette()
    {
        if (tileEditController == null || buttonContainer == null || buttonPrefab == null)
            return;

        for (int i = buttonContainer.childCount - 1; i >= 0; i--)
            Destroy(buttonContainer.GetChild(i).gameObject);

        IReadOnlyList<TilePaletteEntry> palette = tileEditController.GetPalette();
        if (palette == null || palette.Count == 0)
            return;

        foreach (TilePaletteEntry entry in palette)
        {
            if (entry.tile == null) continue;

            GameObject go = Instantiate(buttonPrefab, buttonContainer);
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
    }
}
