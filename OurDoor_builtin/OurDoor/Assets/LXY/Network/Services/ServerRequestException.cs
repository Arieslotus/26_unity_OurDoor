/// <summary>
/// 实现功能：携带服务端业务错误码、消息 ID 和错误说明，避免业务失败被误判为网络异常。
/// </summary>
using System;

namespace OurDoor.LXY.Networking.Services
{
    public sealed class ServerRequestException : Exception
    {
        public ushort MessageId { get; }
        public int Code { get; }

        public ServerRequestException(ushort messageId, int code, string message)
            : base($"服务端拒绝请求，messageId={messageId}, code={code}, message={message}")
        {
            MessageId = messageId;
            Code = code;
        }
    }
}
