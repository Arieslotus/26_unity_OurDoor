namespace OurDoor.LXY.Networking.Protocol
{
    public static class MessageIds
    {
        public const ushort Heartbeat = 1;
        public const ushort GuestLogin = 100;
        public const ushort CreateRoom = 200;
        public const ushort JoinRoom = 201;
        public const ushort LevelAction = 300;
        public const ushort ReadyNextLevel = 301;
        public const ushort RoomReady = 900;
        public const ushort RoomSnapshot = 901;
        public const ushort LevelChanged = 904;
        public const ushort ServerError = 999;
    }
}
