using UnityEngine;

/// <summary>
/// 에디트에서 그리드에 배치하는 몬스터 프리팹. MapData의 prefabId와 매칭됩니다.
/// </summary>
[System.Serializable]
public class MonsterPaletteEntry
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
