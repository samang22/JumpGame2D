using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TileEditController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera editCamera;
    [SerializeField] private Tilemap groundTilemap;           
    [SerializeField] private Tilemap oneWayTilemap;           
    [SerializeField] private Tilemap backgroundTilemap;           

    [Header("Palette")]
    [SerializeField] private List<TilePaletteEntry> palette;
    [SerializeField] private string resourcesPalettePath = "Tiles";
    [SerializeField] private TilePaletteUI paletteUI;

    [Header("Current State")]
    [SerializeField] private TileBase paintTile;    // currently selected tile for painting
    [SerializeField] private string paintTileId;    // ID of the currently selected tile (for save/load)
    [SerializeField] private bool eraseMode;        // true = erase instead of paint

    private Tilemap GetTilemapForLayer(TileLayerType layer)
    {
        switch (layer)
        {
            case TileLayerType.OneWay:
                return oneWayTilemap != null ? oneWayTilemap : groundTilemap;
            case TileLayerType.BackGround:
                return backgroundTilemap != null ? backgroundTilemap : groundTilemap;
            // Gimmick, Hazard 전용 Tilemap을 추가하면 여기에서 분기
            default:
                return groundTilemap;
        }
    }

    void Update()
    {
        if (editCamera == null || groundTilemap == null) return;
        if (Mouse.current == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3Int cellPos = GetMouseCell(mouseScreenPos);

        bool leftPressed = Mouse.current.leftButton.isPressed;
        bool rightPressed = Mouse.current.rightButton.isPressed;

        if (leftPressed)
        {
            if (eraseMode)
                EraseAt(cellPos);
            else
                PaintAt(cellPos);
        }
        else if (rightPressed)
        {
            EraseAt(cellPos);
        }
    }

    Vector3Int GetMouseCell(Vector2 mouseScreenPos)
    {
        Vector3 worldPos = editCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
        return groundTilemap.WorldToCell(worldPos);
    }

    void PaintAt(Vector3Int cell)
    {
        if (paintTile == null) return;

        TilePaletteEntry entry = FindEntryByTile(paintTile);
        Tilemap target = entry != null ? GetTilemapForLayer(entry.layer) : groundTilemap;
        target.SetTile(cell, paintTile);
    }

    void EraseAt(Vector3Int cell)
    {
        groundTilemap.SetTile(cell, null);
        if (oneWayTilemap != null)
            oneWayTilemap.SetTile(cell, null);
        if (backgroundTilemap != null)
            backgroundTilemap.SetTile(cell, null);
    }

    public void SetPaintTile(TileBase tile)
    {
        paintTile = tile;
        paintTileId = FindEntryByTile(tile)?.id ?? "";
    }

    public void SetPaintTileById(string id)
    {
        var entry = FindEntryById(id);
        if (entry == null) return;
        paintTile = entry.tile;
        paintTileId = id;
    }

    public void SetEraseMode(bool on) { eraseMode = on; }

    /// <summary>Returns the current palette list. Creates an empty list if needed.</summary>
    public IReadOnlyList<TilePaletteEntry> GetPalette() => palette ?? (palette = new List<TilePaletteEntry>());

    /// <summary>Loads tiles from Resources into the palette. Intended to be called from a UI button.</summary>
    public void LoadPaletteFromResources()
    {
        if (string.IsNullOrWhiteSpace(resourcesPalettePath))
        {
            Debug.LogWarning("Resources palette path is empty.");
            return;
        }

        TileBase[] tiles = Resources.LoadAll<TileBase>(resourcesPalettePath);
        if (tiles == null || tiles.Length == 0)
        {
            Debug.LogWarning($"No TileBase assets found under Resources/{resourcesPalettePath}. Make sure tiles are placed under a Resources folder.");
            return;
        }

        if (palette == null) palette = new List<TilePaletteEntry>();
        palette.Clear();
        foreach (TileBase t in tiles)
        {
            palette.Add(new TilePaletteEntry
            {
                id = t.name,
                displayName = t.name,
                icon = null,
                tile = t,
                layer = TileLayerType.Solid
            });
        }

        if (paletteUI != null)
            paletteUI.RefreshPalette();

        Debug.Log($"Palette loaded: {palette.Count} tiles (Resources/{resourcesPalettePath})");
    }

    TilePaletteEntry FindEntryByTile(TileBase tile)
    {
        if (tile == null || palette == null) return null;
        foreach (var e in palette)
            if (e.tile == tile) return e;
        return null;
    }

    TilePaletteEntry FindEntryById(string id)
    {
        if (string.IsNullOrEmpty(id) || palette == null) return null;
        foreach (var e in palette)
            if (e.id == id) return e;
        return null;
    }

    public string GetTileIdAt(Vector3Int cell)
    {
        TileBase t = groundTilemap.GetTile(cell);
        if (t == null && oneWayTilemap != null) t = oneWayTilemap.GetTile(cell);
        if (t == null && backgroundTilemap != null) t = backgroundTilemap.GetTile(cell);
        if (t == null) return null;
        var e = FindEntryByTile(t);
        return e?.id;
    }

    public TileBase GetTileAt(Vector3Int cell)
    {
        TileBase t = groundTilemap.GetTile(cell);
        if (t != null) return t;
        if (oneWayTilemap != null)
        {
            t = oneWayTilemap.GetTile(cell);
            if (t != null) return t;
        }
        if (backgroundTilemap != null)
        {
            t = backgroundTilemap.GetTile(cell);
            if (t != null) return t;
        }
        return null;
    }
}