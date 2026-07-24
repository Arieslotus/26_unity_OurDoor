/// <summary>
/// 实现功能：定义主动退出房间和对端离开通知使用的通用协议数据。
/// </summary>
using System;

namespace OurDoor.LXY.Networking.Protocol
{
    [Serializable]
    public sealed class LeaveRoomRequest
    {
        public string roomId;
    }

    [Serializable]
    public sealed class LeaveRoomResponse
    {
        public int code;
        public string message;
        public string roomId;
    }

    [Serializable]
    public sealed class PlayerLeftDto
    {
        public string roomId;
        public string leftUid;
        public string reason;
    }
}
