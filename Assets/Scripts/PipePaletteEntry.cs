using UnityEngine;

/// <summary>
/// 에디트 팔레트의 입구 또는 출구 파이프 프리팹. MapData의 prefabId와 매칭됩니다.
/// </summary>
[System.Serializable]
public class PipePaletteEntry
{
    public string id;
    public string displayName;
    public GameObject prefab;
    public Sprite icon;

    public string GetLabel() => string.IsNullOrEmpty(displayName) ? id : displayName;

    public Sprite GetDisplaySprite()
    {
        if (icon != null) return icon;
        if (prefab == null) return null;
        var sr = prefab.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.sprite : null;
    }
}
