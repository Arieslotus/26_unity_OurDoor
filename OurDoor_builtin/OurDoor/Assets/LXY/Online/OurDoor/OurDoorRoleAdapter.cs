/// <summary>
/// 实现功能：将服务端分配的 Outer、Inner 角色写入 OurDoor 现有 GameManager。
/// </summary>
using System;

public static class OurDoorRoleAdapter
{
    public static void Apply(string role)
    {
        if (GameManager.Instance == null)
            throw new InvalidOperationException("[M2 角色适配] 场景中缺少 GameManager。");

        GameManager.Instance.SetGameMode(GameManager.GameMode.TwoPlayer);
        switch (role)
        {
            case "Outer":
                GameManager.Instance.SetPlayerRole(GameManager.PlayerRole.Outside);
                return;
            case "Inner":
                GameManager.Instance.SetPlayerRole(GameManager.PlayerRole.Inside);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(role),
                    $"[M2 角色适配] 服务端返回未知角色：{role}。");
        }
    }
}
