using System.IO;
using NUnit.Framework;
using OurDoor.LXY.Networking.Core;
using OurDoor.LXY.Networking.Protocol;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class ProtocolCodecTests
    {
        [Test]
        public void EncodeUsesBigEndianHeader()
        {
            var payload = ProtocolCodec.Encode(new NetworkEnvelope(
                0x1234,
                0x01020304,
                MessageType.Request,
                "{}"));

            Assert.That(payload[0], Is.EqualTo(0x12));
            Assert.That(payload[1], Is.EqualTo(0x34));
            Assert.That(payload[2], Is.EqualTo(0x01));
            Assert.That(payload[3], Is.EqualTo(0x02));
            Assert.That(payload[4], Is.EqualTo(0x03));
            Assert.That(payload[5], Is.EqualTo(0x04));
            Assert.That(payload[6], Is.EqualTo((byte)MessageType.Request));
        }

        [Test]
        public void HeartbeatMatchesCrossLanguageWireVector()
        {
            var payload = ProtocolCodec.Encode(new NetworkEnvelope(
                1,
                0x01020304,
                MessageType.Request,
                "{\"sequence\":1}"));
            var frame = PacketFramer.Frame(payload, ProtocolCodec.HeaderSize, 32 * 1024);
            var expected = new byte[]
            {
                0x00, 0x15,
                0x00, 0x01,
                0x01, 0x02, 0x03, 0x04,
                0x00,
                0x7B, 0x22, 0x73, 0x65, 0x71, 0x75, 0x65, 0x6E, 0x63, 0x65, 0x22, 0x3A, 0x31, 0x7D
            };

            Assert.That(frame, Is.EqualTo(expected));
        }

        [Test]
        public void EncodeDecodeRoundTripPreservesEnvelope()
        {
            var source = new NetworkEnvelope(1, 42, MessageType.Response, "{\"sequence\":20}");

            var result = ProtocolCodec.Decode(ProtocolCodec.Encode(source));

            Assert.That(result.MessageId, Is.EqualTo(source.MessageId));
            Assert.That(result.Session, Is.EqualTo(source.Session));
            Assert.That(result.MessageType, Is.EqualTo(source.MessageType));
            Assert.That(result.JsonBody, Is.EqualTo(source.JsonBody));
        }

        [Test]
        public void RequestWithZeroSessionIsRejected()
        {
            Assert.Throws<InvalidDataException>(() => ProtocolCodec.Encode(
                new NetworkEnvelope(1, 0, MessageType.Request, "{}")));
        }

        [Test]
        public void PushWithNonZeroSessionIsRejected()
        {
            Assert.Throws<InvalidDataException>(() => ProtocolCodec.Encode(
                new NetworkEnvelope(1, 1, MessageType.Push, "{}")));
        }
    }
}
