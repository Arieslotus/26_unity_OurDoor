/// <summary>
/// 实现功能：定义双方换关准备请求、响应和服务端原子切换关卡推送。
/// </summary>
using System;

namespace OurDoor.LXY.Networking.Protocol
{
    [Serializable]
    public sealed class ReadyNextLevelRequest
    {
        public string roomId;
        public int levelId;
    }

    [Serializable]
    public sealed class ReadyNextLevelResponse
    {
        public int code;
        public string message;
        public string roomId;
        public int levelId;
        public int readyCount;
        public int revision;
    }

    [Serializable]
    public sealed class LevelChangedDto
    {
        public string roomId;
        public int fromLevelId;
        public int toLevelId;
        public string role;
        public int revision;
        public RoomSnapshotDto snapshot;
    }
}
