using System;
using UnityEngine;

/// <summary>
/// 움직이는 <see cref="GreenTurtleShell"/>에 맞았을 때 몬스터 쪽에서 호출. 즉사 처리는 <paramref name="applyKillFromShell"/>에 위임.
/// </summary>
public static class MonsterGreenShellContact
{
    /// <returns>껍데기가 아니거나 Move가 아니면 false.</returns>
    public static bool TryHandleMovingShellHit(Collision2D col, MonoBehaviour self, Action applyKillFromShell)
    {
        if (self == null || col == null) return false;
        if (GameState.IsMapEditMode || GameState.IsVictory) return false;

        var shell = col.collider != null ? col.collider.GetComponentInParent<GreenTurtleShell>(true) : null;
        if (shell == null) return false;
        if (shell.gameObject == self.gameObject) return false;
        if (shell.GetMoveStateAtContact(col) != GreenTurtleShellMoveState.Move) return false;

        shell.RestoreVelocityAfterMonsterContact();
        applyKillFromShell?.Invoke();
        return true;
    }

    /// <summary>
    /// 껍데기 오브젝트의 <see cref="Collision2D"/>에서 호출.
    /// 클래스 이름이 Koopa / BoongBoong 인 <see cref="IShellKillable"/>만 껍질 쪽에서 즉사 보완(Kinematic 콜백 누락 대비).
    /// </summary>
    public static bool TryHandleShellHitsEnemyFromShellSide(Collision2D col, GreenTurtleShell shell)
    {
        if (shell == null || col == null) return false;
        if (GameState.IsMapEditMode || GameState.IsVictory) return false;
        if (shell.GetMoveStateAtContact(col) != GreenTurtleShellMoveState.Move) return false;

        var killable = FindKoopaOrBoongBoongShellKillable(col.collider);
        if (killable == null) return false;

        var killMb = killable as MonoBehaviour;
        if (killMb != null && killMb.gameObject == shell.gameObject)
            return false;

        shell.RestoreVelocityAfterMonsterContact();
        killable.OnShellKill();
        return true;
    }

    static IShellKillable FindKoopaOrBoongBoongShellKillable(Collider2D other)
    {
        if (other == null) return null;
        var mbs = other.GetComponentsInParent<MonoBehaviour>(true);
        foreach (var mb in mbs)
        {
            if (mb is not IShellKillable sk) continue;
            var typeName = mb.GetType().Name;
            if (typeName == "Koopa" || typeName == "BoongBoong")
                return sk;
        }
        return null;
    }
}
