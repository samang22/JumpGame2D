/// <summary>
/// 이동 중 껍데기 등에 맞았을 때 즉시 Destroy 대신 뒤집힘·낙하 사망 연출을 쓰는 몬스터.
/// </summary>
public interface IShellKillable
{
    void OnShellKill();
}
