using UnityEngine;

/// <summary>
/// 몬스터 사망 연출 중 Ground·Player와의 2D 충돌을 무시해 아래로 그대로 떨어지게 할 때 사용.
/// </summary>
public static class MonsterDeathCollisionUtil
{
    /// <summary>사망 후 바닥 통과 낙하 시 <see cref="Rigidbody2D.gravityScale"/>에 곱해 낙하 속도를 올립니다.</summary>
    public const float DeathFallGravityMultiplier = 1.3f;

    /// <param name="playerRootIfKnown">밟힌 플레이어가 있으면 전달(해당 오브젝트의 콜라이더만 무시). null이면 playerTag로 검색.</param>
    public static void IgnorePlayerAndGround(Collider2D[] myColliders, string playerTag, string groundTag, GameObject playerRootIfKnown = null)
    {
        if (myColliders == null) return;
        IgnoreCollidersWithTag(myColliders, groundTag);
        if (playerRootIfKnown != null)
            IgnoreBetweenSets(myColliders, playerRootIfKnown.GetComponentsInChildren<Collider2D>(true));
        else
            IgnoreCollidersWithTag(myColliders, playerTag);
    }

    static void IgnoreCollidersWithTag(Collider2D[] myColliders, string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        GameObject[] roots;
        try
        {
            roots = GameObject.FindGameObjectsWithTag(tag);
        }
        catch (UnityException)
        {
            return;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null) continue;
            IgnoreBetweenSets(myColliders, roots[i].GetComponentsInChildren<Collider2D>(true));
        }
    }

    static void IgnoreBetweenSets(Collider2D[] myColliders, Collider2D[] otherColliders)
    {
        if (otherColliders == null) return;
        for (int o = 0; o < otherColliders.Length; o++)
        {
            var oc = otherColliders[o];
            if (oc == null || !oc.enabled) continue;
            for (int m = 0; m < myColliders.Length; m++)
            {
                var my = myColliders[m];
                if (my == null || !my.enabled) continue;
                Physics2D.IgnoreCollision(my, oc, true);
            }
        }
    }
}
