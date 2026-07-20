namespace OurDoor.LXY.Networking.Protocol
{
    public sealed class NetworkEnvelope
    {
        public ushort MessageId { get; }
        public uint Session { get; }
        public MessageType MessageType { get; }
        public string JsonBody { get; }

        public NetworkEnvelope(ushort messageId, uint session, MessageType messageType, string jsonBody)
        {
            MessageId = messageId;
            Session = session;
            MessageType = messageType;
            JsonBody = jsonBody ?? string.Empty;
        }
    }
}
