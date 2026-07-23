/// <summary>
/// 实现功能：固定 M3 关卡操作消息 ID 和业务错误码，防止 C# 与 Lua 协议漂移。
/// </summary>
using NUnit.Framework;
using OurDoor.LXY.Networking.Protocol;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class M3ProtocolConstantsTests
    {
        [Test]
        public void LevelActionMessageIdMatchesServerContract()
        {
            Assert.That(MessageIds.LevelAction, Is.EqualTo(300));
        }

        [Test]
        public void LevelActionErrorCodesMatchServerContract()
        {
            Assert.That(ServerErrorCodes.NotInRoom, Is.EqualTo(3001));
            Assert.That(ServerErrorCodes.RoomNotPlaying, Is.EqualTo(3002));
            Assert.That(ServerErrorCodes.LevelMismatch, Is.EqualTo(3003));
            Assert.That(ServerErrorCodes.RoleForbidden, Is.EqualTo(3004));
            Assert.That(ServerErrorCodes.PreconditionNotMet, Is.EqualTo(3005));
            Assert.That(ServerErrorCodes.InvalidAction, Is.EqualTo(3006));
        }

        [Test]
        public void LevelOneStateDefaultsToUnfinished()
        {
            var state = new Level1StateDto();

            Assert.That(state.powerOn, Is.False);
            Assert.That(state.passwordFound, Is.False);
            Assert.That(state.lockOpened, Is.False);
        }
    }
}
