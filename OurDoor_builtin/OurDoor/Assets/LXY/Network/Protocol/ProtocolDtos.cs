using System;

namespace OurDoor.LXY.Networking.Protocol
{
    [Serializable]
    public sealed class HeartbeatMessage
    {
        public int sequence;
        public long clientTimeUtcMs;
    }
}
