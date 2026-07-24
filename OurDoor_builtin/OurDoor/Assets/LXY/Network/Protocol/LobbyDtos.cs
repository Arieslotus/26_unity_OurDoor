/// <summary>
/// 实现功能：定义临时登录、房间请求、房间快照和服务端推送使用的 JSON 数据结构。
/// </summary>
using System;

namespace OurDoor.LXY.Networking.Protocol
{
    [Serializable]
    public sealed class GuestLoginRequest
    {
        public string guestId;
        public string displayName;
        public string clientVersion;
    }

    [Serializable]
    public sealed class GuestLoginResponse
    {
        public int code;
        public string message;
        public string uid;
        public string displayName;
    }

    [Serializable]
    public sealed class CreateRoomRequest
    {
        public int levelId;
        public string rolePreference;
    }

    [Serializable]
    public sealed class CreateRoomResponse
    {
        public int code;
        public string message;
        public string roomId;
        public int levelId;
        public string role;
        public int revision;
    }

    [Serializable]
    public sealed class JoinRoomRequest
    {
        public string roomId;
    }

    [Serializable]
    public sealed class JoinRoomResponse
    {
        public int code;
        public string message;
        public string roomId;
        public int levelId;
        public string role;
        public int revision;
    }

    [Serializable]
    public sealed class RoomPlayerDto
    {
        public string uid;
        public string displayName;
        public string role;
    }

    [Serializable]
    public sealed class RoomSnapshotDto
    {
        public string roomId;
        public string phase;
        public int levelId;
        public int revision;
        public RoomPlayerDto[] players;
        public Level1StateDto level1;
        public Level2StateDto level2;
        public Level3StateDto level3;
    }

    [Serializable]
    public sealed class RoomReadyDto
    {
        public string roomId;
        public int levelId;
        public string role;
        public int revision;
    }

    [Serializable]
    public sealed class ServerErrorDto
    {
        public int code;
        public string message;
    }
}
