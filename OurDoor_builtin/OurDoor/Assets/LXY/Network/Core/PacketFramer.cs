using System;
using System.Collections.Generic;
using System.IO;

namespace OurDoor.LXY.Networking.Core
{
    public sealed class PacketFramer
    {
        public const int LengthPrefixSize = 2;

        private readonly int _minimumPayloadLength;
        private readonly int _maximumPayloadLength;
        private byte[] _buffer;
        private int _count;

        public PacketFramer(int maximumPayloadLength, int minimumPayloadLength)
        {
            if (minimumPayloadLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumPayloadLength));
            if (maximumPayloadLength < minimumPayloadLength || maximumPayloadLength > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maximumPayloadLength));

            _minimumPayloadLength = minimumPayloadLength;
            _maximumPayloadLength = maximumPayloadLength;
            _buffer = new byte[Math.Max(1024, maximumPayloadLength + LengthPrefixSize)];
        }

        public int BufferedByteCount => _count;

        public static byte[] Frame(byte[] payload, int minimumPayloadLength, int maximumPayloadLength)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (payload.Length < minimumPayloadLength || payload.Length > maximumPayloadLength || payload.Length > ushort.MaxValue)
                throw new InvalidDataException($"Payload length {payload.Length} is outside [{minimumPayloadLength}, {maximumPayloadLength}].");

            var packet = new byte[payload.Length + LengthPrefixSize];
            packet[0] = (byte)(payload.Length >> 8);
            packet[1] = (byte)payload.Length;
            Buffer.BlockCopy(payload, 0, packet, LengthPrefixSize, payload.Length);
            return packet;
        }

        public IReadOnlyList<byte[]> Append(byte[] source, int offset, int length)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (offset < 0 || length < 0 || offset > source.Length - length)
                throw new ArgumentOutOfRangeException();
            if (length == 0)
                return Array.Empty<byte[]>();

            EnsureCapacity(_count + length);
            Buffer.BlockCopy(source, offset, _buffer, _count, length);
            _count += length;

            var payloads = new List<byte[]>();
            var consumed = 0;

            while (_count - consumed >= LengthPrefixSize)
            {
                var payloadLength = (_buffer[consumed] << 8) | _buffer[consumed + 1];
                if (payloadLength < _minimumPayloadLength || payloadLength > _maximumPayloadLength)
                {
                    Reset();
                    throw new InvalidDataException($"Invalid payload length: {payloadLength}.");
                }

                var packetLength = LengthPrefixSize + payloadLength;
                if (_count - consumed < packetLength)
                    break;

                var payload = new byte[payloadLength];
                Buffer.BlockCopy(_buffer, consumed + LengthPrefixSize, payload, 0, payloadLength);
                payloads.Add(payload);
                consumed += packetLength;
            }

            if (consumed > 0)
            {
                var remaining = _count - consumed;
                if (remaining > 0)
                    Buffer.BlockCopy(_buffer, consumed, _buffer, 0, remaining);
                _count = remaining;
            }

            return payloads;
        }

        public void Reset()
        {
            _count = 0;
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length)
                return;

            var capacity = _buffer.Length;
            while (capacity < required)
                capacity = checked(capacity * 2);
            Array.Resize(ref _buffer, capacity);
        }
    }
}
