/// <summary>
/// 实现功能：验证 M8 部署端点配置的严格 JSON、Host/Port 校验及 NetworkConfig 应用结果。
/// </summary>
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using OurDoor.LXY.Networking.Deployment;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class M8DeploymentConfigTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "OurDoor-M8-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }

        [TestCase("192.168.1.100", 8888)]
        [TestCase("10.0.0.2", 1)]
        [TestCase("ourdoor-host", 65535)]
        [TestCase("server.lan", 8888)]
        public void ValidEndpointLoads(string host, int port)
        {
            string path = Write(
                $"{{\"host\":\"{host}\",\"port\":{port}}}");

            DeploymentEndpointConfig endpoint =
                DeploymentEndpointConfigLoader.Load(path);

            Assert.That(endpoint.Host, Is.EqualTo(host));
            Assert.That(endpoint.Port, Is.EqualTo(port));
            Assert.That(endpoint.SourcePath, Is.EqualTo(Path.GetFullPath(path)));
        }

        [Test]
        public void MissingFileIsRejected()
        {
            string path = Path.Combine(temporaryDirectory, "missing.json");

            DeploymentConfigException exception =
                Assert.Throws<DeploymentConfigException>(() =>
                    DeploymentEndpointConfigLoader.Load(path));

            StringAssert.Contains("配置文件不存在", exception.Message);
            StringAssert.Contains(Path.GetFullPath(path), exception.Message);
        }

        [TestCase("{\"host\":\"\",\"port\":8888}", "Host 不能为空")]
        [TestCase("{\"host\":\"  \",\"port\":8888}", "Host 不能为空")]
        [TestCase("{\"host\":\"192.168.1.999\",\"port\":8888}", "非法 IPv4")]
        [TestCase("{\"host\":\"http://server\",\"port\":8888}", "非法字符")]
        [TestCase("{\"host\":\"server_name\",\"port\":8888}", "非法字符")]
        [TestCase("{\"host\":\"server\",\"port\":0}", "Port 必须在")]
        [TestCase("{\"host\":\"server\",\"port\":65536}", "Port 必须在")]
        public void InvalidEndpointIsRejected(string json, string expectedMessage)
        {
            DeploymentConfigException exception =
                Assert.Throws<DeploymentConfigException>(() =>
                    DeploymentEndpointConfigLoader.Load(Write(json)));

            StringAssert.Contains(expectedMessage, exception.Message);
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("{\"host\":\"server\",\"port\":}")]
        [TestCase("{\"host\":\"server\",\"port\":8888,}")]
        [TestCase("{\"host\":\"server\",\"port\":8888} trailing")]
        public void InvalidJsonIsRejected(string json)
        {
            Assert.Throws<DeploymentConfigException>(() =>
                DeploymentEndpointConfigLoader.Load(Write(json)));
        }

        [TestCase(
            "{\"host\":\"server\",\"host\":\"other\",\"port\":8888}",
            "重复配置字段：host")]
        [TestCase(
            "{\"host\":\"server\",\"port\":8888,\"port\":9999}",
            "重复配置字段：port")]
        public void DuplicateFieldIsRejected(string json, string expectedMessage)
        {
            DeploymentConfigException exception =
                Assert.Throws<DeploymentConfigException>(() =>
                    DeploymentEndpointConfigLoader.Load(Write(json)));

            StringAssert.Contains(expectedMessage, exception.Message);
        }

        [TestCase("{\"host\":\"server\"}", "缺少配置字段：port")]
        [TestCase("{\"port\":8888}", "缺少配置字段：host")]
        [TestCase("{}", "必须包含 host 和 port")]
        public void MissingFieldIsRejected(string json, string expectedMessage)
        {
            DeploymentConfigException exception =
                Assert.Throws<DeploymentConfigException>(() =>
                    DeploymentEndpointConfigLoader.Load(Write(json)));

            StringAssert.Contains(expectedMessage, exception.Message);
        }

        [Test]
        public void UnknownFieldIsRejected()
        {
            string path = Write(
                "{\"host\":\"server\",\"port\":8888,\"fallback\":\"127.0.0.1\"}");

            DeploymentConfigException exception =
                Assert.Throws<DeploymentConfigException>(() =>
                    DeploymentEndpointConfigLoader.Load(path));

            StringAssert.Contains("未知配置字段：fallback", exception.Message);
        }

        [Test]
        public void LoadedEndpointIsAppliedToNetworkConfig()
        {
            string path = Write(
                "{\"host\":\"192.168.50.20\",\"port\":18888}");
            var networkConfig = new NetworkConfig();

            DeploymentEndpointConfig endpoint =
                DeploymentEndpointConfigLoader.LoadAndApply(
                    networkConfig,
                    path);

            Assert.That(networkConfig.Host, Is.EqualTo(endpoint.Host));
            Assert.That(networkConfig.Port, Is.EqualTo(endpoint.Port));
            Assert.That(networkConfig.Host, Is.EqualTo("192.168.50.20"));
            Assert.That(networkConfig.Port, Is.EqualTo(18888));
        }

        private string Write(string json)
        {
            string path = Path.Combine(
                temporaryDirectory,
                Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }
    }
}
