using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grid overlay using Canvas + RawImage so it works in URP. Creates a World Space canvas with a tiled grid texture.
/// Attach to any GameObject (e.g. empty in Edit scene). Assign Grid to match tilemap; optional.
/// </summary>
public class GridOverlayUI : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private Vector2 cellSize = new Vector2(1f, 1f);
    [SerializeField] private Color lineColor = new Color(0.4f, 0.6f, 0.9f, 0.6f);
    [SerializeField] private int extentCells = 100;
    [SerializeField] private bool onlyInEditMode = true;
    [SerializeField] private Camera overlayCamera;

    private GameObject _root;
    private RawImage _rawImage;
    private Material _mat;
    private Texture2D _tex;

    private void OnEnable()
    {
        overlayCamera = overlayCamera != null ? overlayCamera : Camera.main;
        if (overlayCamera == null) return;
        BuildOverlay();
        UpdateVisibility();
    }

    private void OnDisable()
    {
        if (_root != null) Destroy(_root);
        if (_mat != null && _mat != _rawImage?.defaultMaterial) Destroy(_mat);
        if (_tex != null) Destroy(_tex);
    }

    private void LateUpdate()
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_root == null) return;
        bool show = !onlyInEditMode || !GameState.IsTestPlay;
        if (_root.activeSelf != show)
            _root.SetActive(show);
    }

    private void BuildOverlay()
    {
        Vector2 size = cellSize;
        Vector2 origin = Vector2.zero;
        if (grid != null)
        {
            Vector3 gSize = grid.cellSize;
            size = new Vector2(gSize.x, gSize.y);
            Vector3 p = grid.transform.position;
            origin = new Vector2(p.x, p.y);
        }

        float half = extentCells * Mathf.Max(size.x, size.y) * 0.5f;
        float total = half * 2f;

        _tex = CreateCellTexture();
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        if (shader == null) { Debug.LogWarning("GridOverlayUI: No shader found."); return; }
        _mat = new Material(shader);
        _mat.hideFlags = HideFlags.HideAndDontSave;
        _mat.mainTexture = _tex;
        _mat.mainTextureScale = new Vector2(total / size.x, total / size.y);
        _mat.color = lineColor;

        _root = new GameObject("GridOverlayCanvas");
        _root.transform.position = new Vector3(origin.x, origin.y, 0f);
        _root.transform.rotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one;

        Canvas can = _root.AddComponent<Canvas>();
        can.renderMode = RenderMode.WorldSpace;
        can.worldCamera = overlayCamera;
        can.sortingOrder = 32767;

        RectTransform canRect = _root.GetComponent<RectTransform>();
        canRect.sizeDelta = new Vector2(total, total);
        canRect.pivot = new Vector2(0.5f, 0.5f);
        canRect.anchorMin = new Vector2(0.5f, 0.5f);
        canRect.anchorMax = new Vector2(0.5f, 0.5f);
        canRect.anchoredPosition = Vector2.zero;

        GameObject imgGo = new GameObject("GridImage");
        imgGo.transform.SetParent(_root.transform, false);
        _rawImage = imgGo.AddComponent<RawImage>();
        _rawImage.texture = _tex;
        _rawImage.material = _mat;
        _rawImage.color = lineColor;
        _rawImage.raycastTarget = false;
        RectTransform imgRect = imgGo.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;
    }

    private static Texture2D CreateCellTexture()
    {
        int res = 32;
        var tex = new Texture2D(res, res);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        Color clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            tex.SetPixel(x, y, (x == res - 1 || y == res - 1) ? Color.white : clear);
        tex.Apply();
        return tex;
    }
}
