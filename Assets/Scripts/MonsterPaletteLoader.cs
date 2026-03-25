using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="MonsterPaletteRegistry"/>에서 런타임용 <see cref="MonsterPaletteEntry"/> 리스트를 채웁니다.
/// </summary>
public static class MonsterPaletteLoader
{
    /// <summary>레지스트리 에셋에서 항목을 복사해 list를 채웁니다.</summary>
    public static void LoadFromRegistry(MonsterPaletteRegistry registry, List<MonsterPaletteEntry> list)
    {
        if (list == null) return;
        list.Clear();

        if (registry == null || registry.entries == null)
        {
            Debug.LogWarning("[MonsterPaletteLoader] Registry is null or has no entries list.");
            return;
        }

        foreach (var e in registry.entries)
        {
            if (e == null || e.prefab == null) continue;
            string id = string.IsNullOrEmpty(e.id) ? e.prefab.name : e.id;
            string label = string.IsNullOrEmpty(e.displayName) ? id : e.displayName;
            list.Add(new MonsterPaletteEntry
            {
                id = id,
                displayName = label,
                prefab = e.prefab,
                icon = e.icon
            });
        }

        Debug.Log($"[MonsterPaletteLoader] Loaded {list.Count} monster(s) from registry '{registry.name}'.");
    }

    /// <summary><c>Resources.Load&lt;MonsterPaletteRegistry&gt;(path)</c> 후 <see cref="LoadFromRegistry"/>.</summary>
    /// <param name="resourcePath">확장자 없음 (예: "MonsterPaletteRegistry")</param>
    public static bool TryLoadFromResources(string resourcePath, List<MonsterPaletteEntry> list)
    {
        if (list == null) return false;
        list.Clear();

        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            Debug.LogWarning("[MonsterPaletteLoader] Registry resource path is empty.");
            return false;
        }

        string path = resourcePath.Trim('/');
        var registry = Resources.Load<MonsterPaletteRegistry>(path);
        if (registry == null)
        {
            Debug.LogWarning($"[MonsterPaletteLoader] No MonsterPaletteRegistry at Resources/{path}. Create via Assets → Create → JumpGame → Monster Palette Registry and place under a Resources folder.");
            return false;
        }

        LoadFromRegistry(registry, list);
        return list.Count > 0;
    }
}
