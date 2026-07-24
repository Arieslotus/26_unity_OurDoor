/// <summary>
/// 实现功能：定义客户端需要识别的通用账号与房间业务错误码。
/// </summary>
namespace OurDoor.LXY.Networking.Protocol
{
    public static class ServerErrorCodes
    {
        public const int Success = 0;
        public const int InvalidRequest = 1001;
        public const int AlreadyLoggedIn = 1002;
        public const int GuestAlreadyOnline = 1003;
        public const int NotLoggedIn = 1004;
        public const int AlreadyInRoom = 2001;
        public const int InvalidLevel = 2002;
        public const int RoomNotFound = 2003;
        public const int RoomFull = 2004;
        public const int RoomNotWaiting = 2005;
        public const int NotInRoom = 3001;
        public const int RoomNotPlaying = 3002;
        public const int LevelMismatch = 3003;
        public const int RoleForbidden = 3004;
        public const int PreconditionNotMet = 3005;
        public const int InvalidAction = 3006;
        public const int LevelNotComplete = 3007;
        public const int NoNextLevel = 3008;
        public const int AlreadyMatching = 4001;
        public const int NotMatching = 4002;
        public const int InvalidRolePreference = 4003;
    }
}
