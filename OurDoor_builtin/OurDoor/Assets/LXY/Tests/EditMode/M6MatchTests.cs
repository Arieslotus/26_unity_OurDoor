/// <summary>
/// 实现功能：验证 M6 匹配协议编号及 Matching、取消、匹配成功的严格会话迁移。
/// </summary>
using System;
using NUnit.Framework;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class M6MatchTests
    {
        [Test]
        public void ProtocolConstantsMatchServer()
        {
            Assert.That(MessageIds.MatchRequest, Is.EqualTo(210));
            Assert.That(MessageIds.MatchCancel, Is.EqualTo(211));
            Assert.That(MessageIds.MatchFound, Is.EqualTo(903));
            Assert.That(ServerErrorCodes.AlreadyMatching, Is.EqualTo(4001));
            Assert.That(ServerErrorCodes.NotMatching, Is.EqualTo(4002));
        }

        [Test]
        public void MatchFoundUsesRequestedLevelAndReachesWaitingRoom()
        {
            NetworkSession session = CreateLobbySession();
            session.BeginMatching(2, RolePreferences.Any);
            session.EnterMatchedRoom("R00006", 2, "Outer", 1);

            Assert.That(session.State, Is.EqualTo(OnlineSessionState.WaitingRoom));
            Assert.That(session.MatchingLevelId, Is.Zero);
            Assert.That(session.RoomId, Is.EqualTo("R00006"));
            Assert.That(session.LevelId, Is.EqualTo(2));
            Assert.That(session.Role, Is.EqualTo("Outer"));
            Assert.That(session.Revision, Is.EqualTo(-1));
        }

        [Test]
        public void CancelMatchingReturnsToLobbyWithoutRoomState()
        {
            NetworkSession session = CreateLobbySession();
            session.BeginMatching(3, RolePreferences.Any);
            session.CancelMatching();

            Assert.That(session.State, Is.EqualTo(OnlineSessionState.Lobby));
            Assert.That(session.MatchingLevelId, Is.Zero);
            Assert.That(session.RoomId, Is.Null);
            Assert.That(session.LevelId, Is.Zero);
            Assert.That(session.Role, Is.Null);
        }

        [Test]
        public void MatchFoundForAnotherLevelIsRejected()
        {
            NetworkSession session = CreateLobbySession();
            session.BeginMatching(1, RolePreferences.Any);

            Assert.Throws<InvalidOperationException>(() =>
                session.EnterMatchedRoom("R00006", 2, "Inner", 1));
            Assert.That(session.State, Is.EqualTo(OnlineSessionState.Matching));
            Assert.That(session.MatchingLevelId, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateMatchRequestStateIsRejected()
        {
            NetworkSession session = CreateLobbySession();
            session.BeginMatching(1, RolePreferences.Any);

            Assert.Throws<InvalidOperationException>(() =>
                session.BeginMatching(1, RolePreferences.Any));
        }

        private static NetworkSession CreateLobbySession()
        {
            var session = new NetworkSession();
            session.MarkConnected();
            session.BeginAuthentication();
            session.CompleteAuthentication("guest-a", "PlayerA");
            return session;
        }
    }
}
