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
        public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(connectTimeoutSeconds);
        public TimeSpan RequestTimeout => TimeSpan.FromSeconds(requestTimeoutSeconds);
        public int MaximumPayloadLength => maximumPayloadLength;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("[网络配置] Host 不能为空。");
            if (port <= 0 || port > ushort.MaxValue)
                throw new InvalidOperationException($"[网络配置] Port 超出范围：{port}。");
            if (connectTimeoutSeconds <= 0f)
                throw new InvalidOperationException(
                    $"[网络配置] Connect Timeout Seconds 必须大于 0，当前值：{connectTimeoutSeconds}。");
            if (requestTimeoutSeconds <= 0f)
                throw new InvalidOperationException(
                    $"[网络配置] Request Timeout Seconds 必须大于 0，当前值：{requestTimeoutSeconds}。");
            if (maximumPayloadLength < Protocol.ProtocolCodec.HeaderSize ||
                maximumPayloadLength > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"[网络配置] Maximum Payload Length 必须在 " +
                    $"[{Protocol.ProtocolCodec.HeaderSize}, {ushort.MaxValue}] 内，" +
                    $"当前值：{maximumPayloadLength}。");
            }
        }

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
