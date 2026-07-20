using System;
using UnityEngine;

namespace OurDoor.LXY.Networking
{
    [Serializable]
    public sealed class NetworkConfig
    {
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 8888;
        [SerializeField] private float connectTimeoutSeconds = 5f;
        [SerializeField] private float requestTimeoutSeconds = 3f;
        [SerializeField] private int maximumPayloadLength = 32 * 1024;

        public string Host => host;
        public int Port => port;
        public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(Mathf.Max(0.1f, connectTimeoutSeconds));
        public TimeSpan RequestTimeout => TimeSpan.FromSeconds(Mathf.Max(0.1f, requestTimeoutSeconds));
        public int MaximumPayloadLength => Mathf.Clamp(maximumPayloadLength, Protocol.ProtocolCodec.HeaderSize, ushort.MaxValue);

        public void SetEndpoint(string newHost, int newPort)
        {
            if (string.IsNullOrWhiteSpace(newHost))
                throw new ArgumentException("Host is required.", nameof(newHost));
            if (newPort <= 0 || newPort > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(newPort));
            host = newHost;
            port = newPort;
        }
    }
}
