/// <summary>
/// 实现功能：定义按关卡请求匹配、取消匹配及匹配成功推送使用的 JSON 数据结构。
/// </summary>
using System;

namespace OurDoor.LXY.Networking.Protocol
{
    [Serializable]
    public sealed class MatchRequest
    {
        public int levelId;
    }

    [Serializable]
    public sealed class MatchRequestResponse
    {
        public int code;
        public string message;
        public bool queued;
        public int levelId;
    }

    [Serializable]
    public sealed class MatchCancelRequest
    {
    }

    [Serializable]
    public sealed class MatchCancelResponse
    {
        public int code;
        public string message;
        public bool removed;
        public int levelId;
    }

    [Serializable]
    public sealed class MatchFoundDto
    {
        public string roomId;
        public int levelId;
        public string role;
        public int revision;
    }
}
