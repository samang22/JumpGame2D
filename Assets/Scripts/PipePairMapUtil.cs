using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 데이터의 pipeEntrances / pipeExits를 인스턴스화합니다. 입구-출구 연결은 GreenPipePortal이 워프 직전 pairId로 해결합니다.
/// </summary>
public static class PipePairMapUtil
{
    public static void ClearRoots(Transform entranceRoot, Transform exitRoot)
    {
        if (entranceRoot != null)
        {
            for (int i = entranceRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(entranceRoot.GetChild(i).gameObject);
        }
        if (exitRoot != null)
        {
            for (int i = exitRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(exitRoot.GetChild(i).gameObject);
        }
    }

    public static void ApplyFromMapData(
        MapData data,
        List<PipePaletteEntry> entrancePalette,
        List<PipePaletteEntry> exitPalette,
        Transform placedEntrancesRoot,
        Transform placedExitsRoot)
    {
        PipePairCache.Clear();
        ClearRoots(placedEntrancesRoot, placedExitsRoot);

        if (data == null || placedEntrancesRoot == null || placedExitsRoot == null)
            return;

        if (data.pipeEntrances == null) data.pipeEntrances = new List<PlacedPipeEntranceData>();
        if (data.pipeExits == null) data.pipeExits = new List<PlacedPipeExitData>();

        foreach (var p in data.pipeExits)
        {
            if (p == null || string.IsNullOrEmpty(p.prefabId) || string.IsNullOrEmpty(p.pairId)) continue;
            var entry = FindEntry(exitPalette, p.prefabId);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[PipePairMapUtil] Exit prefab not in palette: {p.prefabId}");
                continue;
            }

            var go = Object.Instantiate(entry.prefab, new Vector3(p.x, p.y, 0f), Quaternion.identity, placedExitsRoot);
            var marker = go.GetComponent<PlacedPipeExitEditMarker>();
            if (marker == null) marker = go.AddComponent<PlacedPipeExitEditMarker>();
            marker.paletteId = entry.id;
            marker.pairId = p.pairId;
        }

        foreach (var p in data.pipeEntrances)
        {
            if (p == null || string.IsNullOrEmpty(p.prefabId) || string.IsNullOrEmpty(p.pairId)) continue;
            var entry = FindEntry(entrancePalette, p.prefabId);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[PipePairMapUtil] Entrance prefab not in palette: {p.prefabId}");
                continue;
            }

            var go = Object.Instantiate(entry.prefab, new Vector3(p.x, p.y, 0f), Quaternion.identity, placedEntrancesRoot);
            var marker = go.GetComponent<PlacedPipeEntranceEditMarker>();
            if (marker == null) marker = go.AddComponent<PlacedPipeEntranceEditMarker>();
            marker.paletteId = entry.id;
            marker.pairId = p.pairId;

            if (go.GetComponentInChildren<GreenPipePortal>(true) == null)
                Debug.LogWarning($"[PipePairMapUtil] Entrance prefab has no GreenPipePortal: {p.prefabId}");
        }
    }

    static PipePaletteEntry FindEntry(List<PipePaletteEntry> list, string id)
    {
        if (string.IsNullOrEmpty(id) || list == null) return null;
        foreach (var e in list)
            if (e != null && e.id == id) return e;
        return null;
    }
}
