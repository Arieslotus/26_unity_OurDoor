/// <summary>
/// 实现功能：定义客户端从连接、登录到进入房间的明确会话状态。
/// </summary>
namespace OurDoor.LXY.Networking.Session
{
    public enum OnlineSessionState
    {
        Disconnected,
        Connected,
        Authenticating,
        Lobby,
        WaitingRoom,
        LoadingLevel,
        Playing
    }
}
