using UnityEngine;

/// <summary>
/// 프로젝트/에디터에서 레이어 쌍 충돌이 꺼져 있어도 몬스터(Enemy 레이어)끼리는 충돌하도록 보장합니다.
/// </summary>
static class Physics2DMonsterCollisionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void EnsureMonsterVsMonsterCollision()
    {
        int enemy = LayerMask.NameToLayer("Enemy");
        if (enemy >= 0)
            Physics2D.IgnoreLayerCollision(enemy, enemy, false);

        Physics2D.IgnoreLayerCollision(0, 0, false);
    }
}
