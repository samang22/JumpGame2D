using UnityEngine;
using UnityEngine.Tilemaps;
using System.Reflection;

[System.Serializable]
public class TilePaletteEntry
{
    public string id;           // identifier used for save/load and behavior branching ("Ground", "OneWay", "Rail", etc.)
    public string displayName;  // label shown in UI (optional)
    public Sprite icon;         // palette button icon (optional, falls back to tile sprite)
    public TileBase tile;       // actual Tile/RuleTile asset
    public TileLayerType layer; // which Tilemap layer this entry paints to

    /// <summary>Returns the sprite used for UI (icon if set, otherwise derived from the tile).</summary>
    public Sprite GetDisplaySprite()
    {
        if (icon != null) return icon;
        return TilePaletteEntry.GetSpriteFromTileBase(tile);
    }

    /// <summary>Extracts a sprite from a TileBase. Supports Tile and RuleTile (via reflection).</summary>
    public static Sprite GetSpriteFromTileBase(TileBase tileBase)
    {
        if (tileBase == null) return null;

        if (tileBase is Tile tile)
            return tile.sprite;

        // RuleTile (and similar): try to get the internal m_DefaultSprite via reflection
        var type = tileBase.GetType();
        var field = type.GetField("m_DefaultSprite", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.GetValue(tileBase) is Sprite s)
            return s;
        return null;
    }
}

public enum TileLayerType
{
    Solid,      // Ground: solid tiles the player walks on -> groundTilemap
    OneWay,     // one-way platforms: pass from below, stand on top -> oneWayTilemap
    BackGround, // decorative tiles with no collision
    Gimmick,    // tiles used only by other systems/objects (rails, triggers, etc.)
    Hazard      // harmful tiles (damage/kill on contact)
}

public static class TileLayerTypeDisplay
{
    public static string GetDisplayName(TileLayerType layer)
    {
        switch (layer)
        {
            case TileLayerType.Solid: return "Ground";
            case TileLayerType.OneWay: return "One Way";
            case TileLayerType.BackGround: return "Background";
            case TileLayerType.Gimmick: return "Gimmick";
            case TileLayerType.Hazard: return "Hazard";
            default: return layer.ToString();
        }
    }
}