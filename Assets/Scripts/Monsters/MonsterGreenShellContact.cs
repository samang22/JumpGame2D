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
}
