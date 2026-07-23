using System;
using NUnit.Framework;

namespace OurDoor.LXY.Networking.Tests
{
    /// <summary>
    /// 实现功能：验证网络端点配置拒绝非法输入，不自动纠正错误。
    /// </summary>
    public sealed class NetworkConfigTests
    {
        [TestCase("")]
        [TestCase(" ")]
        public void EmptyHostIsRejected(string host)
        {
            var config = new NetworkConfig();

            Assert.Throws<ArgumentException>(() => config.SetEndpoint(host, 8888));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(65536)]
        public void InvalidPortIsRejected(int port)
        {
            var config = new NetworkConfig();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                config.SetEndpoint("127.0.0.1", port));
        }

        [Test]
        public void ValidEndpointPassesValidation()
        {
            var config = new NetworkConfig();
            config.SetEndpoint("127.0.0.1", 8888);

            Assert.DoesNotThrow(config.Validate);
        }
    }
}
