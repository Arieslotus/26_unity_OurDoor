/// <summary>
/// 实现功能：验证 M2 会话状态迁移、房间一致性和快照 revision 去重规则。
/// </summary>
using System;
using NUnit.Framework;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class NetworkSessionTests
    {
        [Test]
        public void CompleteRoomFlowReachesPlaying()
        {
            var session = CreateLobbySession();
            session.EnterWaitingRoom("R00001", 2, "Outer", 0);

            Assert.That(session.ApplySnapshot(CreateSnapshot("R00001", 2, 1)), Is.True);
            session.MarkRoomReady(new RoomReadyDto
            {
                roomId = "R00001",
                levelId = 2,
                role = "Outer",
                revision = 1
            });
            session.MarkPlaying();

            Assert.That(session.State, Is.EqualTo(OnlineSessionState.Playing));
            Assert.That(session.RoomId, Is.EqualTo("R00001"));
            Assert.That(session.LevelId, Is.EqualTo(2));
            Assert.That(session.Role, Is.EqualTo("Outer"));
            Assert.That(session.Revision, Is.EqualTo(1));
        }

        [Test]
        public void RoomReadyWithoutInitialSnapshotIsRejected()
        {
            var session = CreateLobbySession();
            session.EnterWaitingRoom("R00001", 1, "Inner", 0);

            Assert.Throws<InvalidOperationException>(() =>
                session.MarkRoomReady(new RoomReadyDto
                {
                    roomId = "R00001",
                    levelId = 1,
                    role = "Inner",
                    revision = 0
                }));
        }

        [Test]
        public void DuplicateOrOlderSnapshotIsIgnored()
        {
            var session = CreateLobbySession();
            session.EnterWaitingRoom("R00001", 3, "Outer", 0);

            Assert.That(session.ApplySnapshot(CreateSnapshot("R00001", 3, 1)), Is.True);
            Assert.That(session.ApplySnapshot(CreateSnapshot("R00001", 3, 1)), Is.False);
            Assert.That(session.ApplySnapshot(CreateSnapshot("R00001", 3, 0)), Is.False);
            Assert.That(session.Revision, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotForAnotherRoomIsRejected()
        {
            var session = CreateLobbySession();
            session.EnterWaitingRoom("R00001", 1, "Outer", 0);

            Assert.Throws<InvalidOperationException>(() =>
                session.ApplySnapshot(CreateSnapshot("R00002", 1, 1)));
        }

        [Test]
        public void RoomOperationBeforeLoginIsRejected()
        {
            var session = new NetworkSession();
            session.MarkConnected();

            Assert.Throws<InvalidOperationException>(() =>
                session.EnterWaitingRoom("R00001", 1, "Outer", 0));
        }

        private static NetworkSession CreateLobbySession()
        {
            var session = new NetworkSession();
            session.MarkConnected();
            session.BeginAuthentication();
            session.CompleteAuthentication("guest-a", "PlayerA");
            return session;
        }

        private static RoomSnapshotDto CreateSnapshot(
            string roomId,
            int levelId,
            int revision)
        {
            return new RoomSnapshotDto
            {
                roomId = roomId,
                phase = "playing",
                levelId = levelId,
                revision = revision,
                players = new[]
                {
                    new RoomPlayerDto
                    {
                        uid = "guest-a",
                        displayName = "PlayerA",
                        role = "Outer"
                    },
                    new RoomPlayerDto
                    {
                        uid = "guest-b",
                        displayName = "PlayerB",
                        role = "Inner"
                    }
                }
            };
        }
    }
}
