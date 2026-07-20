using System;
using System.IO;
using NUnit.Framework;
using OurDoor.LXY.Networking.Core;
using OurDoor.LXY.Networking.Protocol;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class PacketFramerTests
    {
        private const int MaxPayload = 32 * 1024;

        [Test]
        public void CompletePacketProducesOnePayload()
        {
            var payload = CreatePayload(1, 10, "{\"sequence\":1}");
            var packet = PacketFramer.Frame(payload, ProtocolCodec.HeaderSize, MaxPayload);
            var framer = CreateFramer();

            var result = framer.Append(packet, 0, packet.Length);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(payload));
            Assert.That(framer.BufferedByteCount, Is.Zero);
        }

        [Test]
        public void SplitHeaderAndBodyAreRetainedUntilComplete()
        {
            var payload = CreatePayload(1, 11, "{\"sequence\":2}");
            var packet = PacketFramer.Frame(payload, ProtocolCodec.HeaderSize, MaxPayload);
            var framer = CreateFramer();

            Assert.That(framer.Append(packet, 0, 1), Is.Empty);
            Assert.That(framer.Append(packet, 1, 4), Is.Empty);
            var result = framer.Append(packet, 5, packet.Length - 5);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(payload));
        }

        [Test]
        public void StickyPacketsProduceMultiplePayloads()
        {
            var first = CreatePayload(1, 12, "{\"sequence\":3}");
            var second = CreatePayload(1, 13, "{\"sequence\":4}");
            var firstPacket = PacketFramer.Frame(first, ProtocolCodec.HeaderSize, MaxPayload);
            var secondPacket = PacketFramer.Frame(second, ProtocolCodec.HeaderSize, MaxPayload);
            var combined = new byte[firstPacket.Length + secondPacket.Length];
            Buffer.BlockCopy(firstPacket, 0, combined, 0, firstPacket.Length);
            Buffer.BlockCopy(secondPacket, 0, combined, firstPacket.Length, secondPacket.Length);

            var result = CreateFramer().Append(combined, 0, combined.Length);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0], Is.EqualTo(first));
            Assert.That(result[1], Is.EqualTo(second));
        }

        [Test]
        public void CompletePacketAndPartialNextPacketRetainRemainder()
        {
            var first = CreatePayload(1, 14, "{\"sequence\":5}");
            var second = CreatePayload(1, 15, "{\"sequence\":6}");
            var firstPacket = PacketFramer.Frame(first, ProtocolCodec.HeaderSize, MaxPayload);
            var secondPacket = PacketFramer.Frame(second, ProtocolCodec.HeaderSize, MaxPayload);
            const int partialLength = 5;
            var combined = new byte[firstPacket.Length + partialLength];
            Buffer.BlockCopy(firstPacket, 0, combined, 0, firstPacket.Length);
            Buffer.BlockCopy(secondPacket, 0, combined, firstPacket.Length, partialLength);
            var framer = CreateFramer();

            var firstResult = framer.Append(combined, 0, combined.Length);
            var secondResult = framer.Append(secondPacket, partialLength, secondPacket.Length - partialLength);

            Assert.That(firstResult, Has.Count.EqualTo(1));
            Assert.That(firstResult[0], Is.EqualTo(first));
            Assert.That(secondResult, Has.Count.EqualTo(1));
            Assert.That(secondResult[0], Is.EqualTo(second));
        }

        [TestCase(0)]
        [TestCase(6)]
        public void InvalidPayloadLengthThrows(int invalidLength)
        {
            var input = new[] { (byte)(invalidLength >> 8), (byte)invalidLength };
            var framer = CreateFramer();

            Assert.Throws<InvalidDataException>(() => framer.Append(input, 0, input.Length));
            Assert.That(framer.BufferedByteCount, Is.Zero);
        }

        private static PacketFramer CreateFramer()
        {
            return new PacketFramer(MaxPayload, ProtocolCodec.HeaderSize);
        }

        private static byte[] CreatePayload(ushort messageId, uint session, string body)
        {
            return ProtocolCodec.Encode(new NetworkEnvelope(messageId, session, MessageType.Request, body));
        }
    }
}
