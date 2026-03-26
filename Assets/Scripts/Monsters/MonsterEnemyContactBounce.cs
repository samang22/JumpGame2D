using UnityEngine;

/// <summary>
/// Enemy 태그 루트끼리 맞닿았을 때 이동 방향을 바꿀 때 공통으로 쓰는 유틸.
/// </summary>
public static class MonsterEnemyContactBounce
{
    public const float CooldownSeconds = 0.18f;

    public static GameObject FindEnemyRoot(GameObject go, string enemyTag)
    {
        if (go == null || string.IsNullOrEmpty(enemyTag)) return null;
        for (Transform t = go.transform; t != null; t = t.parent)
        {
            if (t.CompareTag(enemyTag))
                return t.gameObject;
        }
        // GreenTurtleShell 프리팹이 Untagged인 경우가 있어, 몬스터로 취급
        var shell = go.GetComponentInParent<GreenTurtleShell>();
        if (shell != null)
            return shell.gameObject;
        return null;
    }
}
