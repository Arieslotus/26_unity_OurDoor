using System;
using System.IO;
using System.Text;

namespace OurDoor.LXY.Networking.Protocol
{
    public static class ProtocolCodec
    {
        public const int HeaderSize = 7;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(NetworkEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            ValidateHeader(envelope.MessageId, envelope.Session, envelope.MessageType);

            var body = Utf8.GetBytes(envelope.JsonBody);
            var payload = new byte[HeaderSize + body.Length];

            WriteUInt16(payload, 0, envelope.MessageId);
            WriteUInt32(payload, 2, envelope.Session);
            payload[6] = (byte)envelope.MessageType;
            if (body.Length > 0)
                Buffer.BlockCopy(body, 0, payload, HeaderSize, body.Length);

            return payload;
        }

        public static NetworkEnvelope Decode(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (payload.Length < HeaderSize)
                throw new InvalidDataException($"Payload requires at least {HeaderSize} bytes.");

            var messageId = ReadUInt16(payload, 0);
            var session = ReadUInt32(payload, 2);
            var messageType = (MessageType)payload[6];
            ValidateHeader(messageId, session, messageType);

            string jsonBody;
            try
            {
                jsonBody = payload.Length == HeaderSize
                    ? string.Empty
                    : Utf8.GetString(payload, HeaderSize, payload.Length - HeaderSize);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("JSON body is not valid UTF-8.", exception);
            }

            return new NetworkEnvelope(messageId, session, messageType, jsonBody);
        }

        private static void ValidateHeader(ushort messageId, uint session, MessageType messageType)
        {
            if (messageId == 0)
                throw new InvalidDataException("Message ID 0 is reserved.");
            if (!Enum.IsDefined(typeof(MessageType), messageType))
                throw new InvalidDataException($"Unknown message type: {(byte)messageType}.");
            if ((messageType == MessageType.Request || messageType == MessageType.Response) && session == 0)
                throw new InvalidDataException("Request and response messages require a non-zero session.");
            if (messageType == MessageType.Push && session != 0)
                throw new InvalidDataException("Push messages require session 0.");
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return ((uint)buffer[offset] << 24)
                   | ((uint)buffer[offset + 1] << 16)
                   | ((uint)buffer[offset + 2] << 8)
                   | buffer[offset + 3];
        }
    }
}
