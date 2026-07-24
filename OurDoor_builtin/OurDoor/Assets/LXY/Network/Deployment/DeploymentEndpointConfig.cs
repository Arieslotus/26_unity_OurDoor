/// <summary>
/// 实现功能：严格读取发布目录中的联网端点配置，拒绝缺失、重复、未知字段及非法 Host/Port。
/// </summary>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace OurDoor.LXY.Networking.Deployment
{
    public sealed class DeploymentEndpointConfig
    {
        public string Host { get; }
        public int Port { get; }
        public string SourcePath { get; }

        internal DeploymentEndpointConfig(string host, int port, string sourcePath)
        {
            Host = host;
            Port = port;
            SourcePath = sourcePath;
        }

        public override string ToString()
        {
            return $"{Host}:{Port}";
        }
    }

    public sealed class DeploymentConfigException : Exception
    {
        public string ConfigPath { get; }

        public DeploymentConfigException(
            string configPath,
            string message,
            Exception innerException = null)
            : base($"[部署配置] {message}，path={configPath}。", innerException)
        {
            ConfigPath = configPath;
        }
    }

    public static class DeploymentEndpointConfigLoader
    {
        public const string FileName = "ourdoor-network.json";

        public static string GetDefaultPath()
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", FileName));
        }

        public static DeploymentEndpointConfig LoadDefault()
        {
            return Load(GetDefaultPath());
        }

        public static DeploymentEndpointConfig Load(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("配置文件路径不能为空。", nameof(configPath));

            string fullPath = Path.GetFullPath(configPath);
            if (!File.Exists(fullPath))
            {
                throw new DeploymentConfigException(
                    fullPath,
                    "配置文件不存在");
            }

            string json;
            try
            {
                json = File.ReadAllText(
                    fullPath,
                    new UTF8Encoding(false, true));
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is DecoderFallbackException)
            {
                throw new DeploymentConfigException(
                    fullPath,
                    "无法按严格 UTF-8 读取配置文件",
                    exception);
            }

            try
            {
                ParsedEndpoint parsed = new StrictEndpointJsonParser(json).Parse();
                ValidateHost(parsed.Host);
                ValidatePort(parsed.Port);
                return new DeploymentEndpointConfig(
                    parsed.Host,
                    parsed.Port,
                    fullPath);
            }
            catch (DeploymentConfigException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is ArgumentException)
            {
                throw new DeploymentConfigException(
                    fullPath,
                    exception.Message,
                    exception);
            }
        }

        public static DeploymentEndpointConfig LoadAndApply(
            NetworkConfig networkConfig,
            string configPath)
        {
            if (networkConfig == null)
                throw new ArgumentNullException(nameof(networkConfig));

            DeploymentEndpointConfig endpoint = Load(configPath);
            networkConfig.SetEndpoint(endpoint.Host, endpoint.Port);
            networkConfig.Validate();
            return endpoint;
        }

        public static void ValidateHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host 不能为空。", nameof(host));
            if (!string.Equals(host, host.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Host 首尾不能包含空白字符。", nameof(host));
            if (host.Length > 253)
                throw new ArgumentException(
                    $"Host 长度不能超过 253，当前={host.Length}。",
                    nameof(host));

            string[] ipv4Parts = host.Split('.');
            if (ipv4Parts.Length == 4 && IsValidIpv4(ipv4Parts))
                return;

            bool numericOrDotOnly = true;
            for (int index = 0; index < host.Length; index++)
            {
                char character = host[index];
                if ((character < '0' || character > '9') && character != '.')
                {
                    numericOrDotOnly = false;
                    break;
                }
            }
            if (numericOrDotOnly)
            {
                throw new ArgumentException(
                    $"Host 是非法 IPv4：{host}。",
                    nameof(host));
            }

            string[] labels = host.Split('.');
            for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                string label = labels[labelIndex];
                if (label.Length < 1 || label.Length > 63)
                {
                    throw new ArgumentException(
                        $"Host 标签长度必须在 1-63，labelIndex={labelIndex}。",
                        nameof(host));
                }
                if (!IsAsciiLetterOrDigit(label[0]) ||
                    !IsAsciiLetterOrDigit(label[label.Length - 1]))
                {
                    throw new ArgumentException(
                        $"Host 标签必须以字母或数字开头和结尾，label={label}。",
                        nameof(host));
                }

                for (int characterIndex = 0;
                     characterIndex < label.Length;
                     characterIndex++)
                {
                    char character = label[characterIndex];
                    if (!IsAsciiLetterOrDigit(character) && character != '-')
                    {
                        throw new ArgumentException(
                            $"Host 包含非法字符，label={label}, char={character}。",
                            nameof(host));
                    }
                }
            }
        }

        public static void ValidatePort(int port)
        {
            if (port < 1 || port > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(port),
                    $"Port 必须在 1-65535，当前={port}。");
            }
        }

        private static bool IsValidIpv4(string[] parts)
        {
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index];
                if (part.Length == 0 || part.Length > 3)
                    return false;
                for (int characterIndex = 0;
                     characterIndex < part.Length;
                     characterIndex++)
                {
                    if (part[characterIndex] < '0' ||
                        part[characterIndex] > '9')
                    {
                        return false;
                    }
                }
                if (!int.TryParse(
                        part,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int value) ||
                    value > 255)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsAsciiLetterOrDigit(char character)
        {
            return (character >= 'a' && character <= 'z') ||
                   (character >= 'A' && character <= 'Z') ||
                   (character >= '0' && character <= '9');
        }

        private sealed class ParsedEndpoint
        {
            public string Host;
            public int Port;
        }

        private sealed class StrictEndpointJsonParser
        {
            private readonly string json;
            private int index;

            public StrictEndpointJsonParser(string json)
            {
                this.json = json ?? throw new ArgumentNullException(nameof(json));
            }

            public ParsedEndpoint Parse()
            {
                if (json.Length > 0 && json[0] == '\uFEFF')
                    index++;

                SkipWhitespace();
                Expect('{');
                SkipWhitespace();

                var seen = new HashSet<string>(StringComparer.Ordinal);
                string host = null;
                int? port = null;

                if (TryConsume('}'))
                    throw Error("配置必须包含 host 和 port");

                while (true)
                {
                    string propertyName = ParseString();
                    if (!seen.Add(propertyName))
                        throw Error($"存在重复配置字段：{propertyName}");
                    if (propertyName != "host" && propertyName != "port")
                        throw Error($"存在未知配置字段：{propertyName}");

                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    if (propertyName == "host")
                        host = ParseString();
                    else
                        port = ParseInteger();

                    SkipWhitespace();
                    if (TryConsume('}'))
                        break;
                    Expect(',');
                    SkipWhitespace();
                    if (Peek() == '}')
                        throw Error("最后一个配置字段后不能有逗号");
                }

                SkipWhitespace();
                if (index != json.Length)
                    throw Error("JSON 对象结束后存在多余内容");
                if (!seen.Contains("host"))
                    throw Error("缺少配置字段：host");
                if (!seen.Contains("port"))
                    throw Error("缺少配置字段：port");

                return new ParsedEndpoint
                {
                    Host = host,
                    Port = port.Value
                };
            }

            private string ParseString()
            {
                SkipWhitespace();
                Expect('"');
                var builder = new StringBuilder();
                while (index < json.Length)
                {
                    char character = json[index++];
                    if (character == '"')
                        return builder.ToString();
                    if (character < 0x20)
                        throw Error("字符串包含未转义控制字符");
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (index >= json.Length)
                        throw Error("字符串转义不完整");
                    char escaped = json[index++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeEscape());
                            break;
                        default:
                            throw Error($"未知字符串转义：\\{escaped}");
                    }
                }

                throw Error("字符串缺少结束引号");
            }

            private char ParseUnicodeEscape()
            {
                if (index + 4 > json.Length)
                    throw Error("Unicode 转义必须包含四个十六进制字符");

                int value = 0;
                for (int count = 0; count < 4; count++)
                {
                    char character = json[index++];
                    int digit;
                    if (character >= '0' && character <= '9')
                        digit = character - '0';
                    else if (character >= 'a' && character <= 'f')
                        digit = character - 'a' + 10;
                    else if (character >= 'A' && character <= 'F')
                        digit = character - 'A' + 10;
                    else
                        throw Error($"Unicode 转义包含非法字符：{character}");
                    value = value * 16 + digit;
                }
                return (char)value;
            }

            private int ParseInteger()
            {
                int start = index;
                if (Peek() == '-')
                    index++;
                int digitsStart = index;
                while (index < json.Length &&
                       json[index] >= '0' &&
                       json[index] <= '9')
                {
                    index++;
                }
                if (digitsStart == index)
                    throw Error("port 必须是 JSON 整数");
                if (index - digitsStart > 1 && json[digitsStart] == '0')
                    throw Error("port 不能包含前导零");

                string valueText = json.Substring(start, index - start);
                if (!int.TryParse(
                        valueText,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out int value))
                {
                    throw Error($"port 超出 Int32 范围：{valueText}");
                }
                return value;
            }

            private void SkipWhitespace()
            {
                while (index < json.Length)
                {
                    char character = json[index];
                    if (character != ' ' &&
                        character != '\t' &&
                        character != '\r' &&
                        character != '\n')
                    {
                        return;
                    }
                    index++;
                }
            }

            private char Peek()
            {
                return index < json.Length ? json[index] : '\0';
            }

            private bool TryConsume(char expected)
            {
                if (Peek() != expected)
                    return false;
                index++;
                return true;
            }

            private void Expect(char expected)
            {
                if (!TryConsume(expected))
                {
                    throw Error(
                        $"期望字符 '{expected}'，实际='{Peek()}'");
                }
            }

            private FormatException Error(string message)
            {
                return new FormatException(
                    $"JSON 格式错误：{message}，index={index}");
            }
        }
    }
}
