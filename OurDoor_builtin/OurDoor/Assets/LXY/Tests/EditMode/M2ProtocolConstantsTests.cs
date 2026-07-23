/// <summary>
/// 实现功能：固定 M2 客户端消息 ID 和业务错误码，防止与 Lua 服务端约定漂移。
/// </summary>
using NUnit.Framework;
using OurDoor.LXY.Networking.Protocol;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class M2ProtocolConstantsTests
    {
        [Test]
        public void MessageIdsMatchServerContract()
        {
            Assert.That(MessageIds.GuestLogin, Is.EqualTo(100));
            Assert.That(MessageIds.CreateRoom, Is.EqualTo(200));
            Assert.That(MessageIds.JoinRoom, Is.EqualTo(201));
            Assert.That(MessageIds.RoomReady, Is.EqualTo(900));
            Assert.That(MessageIds.RoomSnapshot, Is.EqualTo(901));
            Assert.That(MessageIds.ServerError, Is.EqualTo(999));
        }

        [Test]
        public void ErrorCodesMatchServerContract()
        {
            Assert.That(ServerErrorCodes.NotLoggedIn, Is.EqualTo(1004));
            Assert.That(ServerErrorCodes.AlreadyInRoom, Is.EqualTo(2001));
            Assert.That(ServerErrorCodes.InvalidLevel, Is.EqualTo(2002));
            Assert.That(ServerErrorCodes.RoomNotFound, Is.EqualTo(2003));
            Assert.That(ServerErrorCodes.RoomFull, Is.EqualTo(2004));
        }
    }
}
