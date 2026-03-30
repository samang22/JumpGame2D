using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// pairId 기준 입구·출구 스탠드 Transform을 한 번 찾아 캐시합니다.
/// </summary>
public static class PipePairCache
{
    struct CachedPair
    {
        public Transform EntranceStand;
        public Transform ExitStand;
    }

    static readonly Dictionary<string, CachedPair> s_cache = new Dictionary<string, CachedPair>();

    public static void Clear() => s_cache.Clear();

    public static bool TryGetExitStand(string pairId, out Transform exitStand)
    {
        exitStand = null;
        if (!EnsurePair(pairId)) return false;
        exitStand = s_cache[pairId].ExitStand;
        return exitStand != null;
    }

    public static bool TryGetEntranceStand(string pairId, out Transform entranceStand)
    {
        entranceStand = null;
        if (!EnsurePair(pairId)) return false;
        entranceStand = s_cache[pairId].EntranceStand;
        return entranceStand != null;
    }

    static bool EnsurePair(string pairId)
    {
        if (string.IsNullOrEmpty(pairId)) return false;

        if (s_cache.TryGetValue(pairId, out var cached))
        {
            if (cached.EntranceStand != null && cached.ExitStand != null)
                return true;
            s_cache.Remove(pairId);
        }

        Transform entranceStand = null;
        Transform exitStand = null;

        var entranceMarkers = Object.FindObjectsByType<PlacedPipeEntranceEditMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in entranceMarkers)
        {
            if (m == null || m.pairId != pairId) continue;
            var portal = m.GetComponentInChildren<GreenPipePortal>(true);
            if (portal == null)
            {
                Debug.LogWarning($"[PipePairCache] 입구에 GreenPipePortal 없음: {m.name}");
                continue;
            }
            entranceStand = portal.GetEntranceLandingTransformForPairing();
            break;
        }

        var exitMarkers = Object.FindObjectsByType<PlacedPipeExitEditMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in exitMarkers)
        {
            if (m == null || m.pairId != pairId) continue;
            var portal = m.GetComponent<GreenPipePortal>();
            if (portal != null && portal.Role == GreenPipePortalRole.Exit)
                exitStand = portal.GetExitLandingTransformForPairing();
            else
            {
                var ps = m.GetComponent<PipeExitStand>();
                exitStand = ps != null ? ps.GetStandPoint() : m.transform;
            }
            break;
        }

        if (entranceStand != null && exitStand != null)
        {
            s_cache[pairId] = new CachedPair { EntranceStand = entranceStand, ExitStand = exitStand };
            return true;
        }

        return false;
    }
}
