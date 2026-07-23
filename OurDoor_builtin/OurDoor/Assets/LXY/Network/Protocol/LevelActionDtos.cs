/// <summary>
/// 实现功能：定义关卡操作请求、服务端处理结果和第一关权威状态的数据结构。
/// </summary>
using System;

namespace OurDoor.LXY.Networking.Protocol
{
    [Serializable]
    public sealed class LevelActionRequest
    {
        public string roomId;
        public int levelId;
        public string action;
        public bool boolValue;
        public string clientActionId;
    }

    [Serializable]
    public sealed class LevelActionResponse
    {
        public int code;
        public string message;
        public string clientActionId;
        public int revision;
        public bool duplicate;
        public bool changed;
    }

    [Serializable]
    public sealed class Level1StateDto
    {
        public bool powerOn;
        public bool passwordFound;
        public bool lockOpened;
    }

    [Serializable]
    public sealed class Level2StateDto
    {
        public bool keyFound;
        public bool keyLanded;
        public bool boxBuilt;
        public bool doorOpened;
    }

    [Serializable]
    public sealed class Level3StateDto
    {
        public bool passwordSuccess;
        public bool metalFound;
        public bool metalReceived;
        public bool wireFound;
        public bool doorOpened;
    }
}
