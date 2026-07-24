/// <summary>
/// 实现功能：验证 M5 协议常量、对端退出回大厅和本端断线清空会话。
/// </summary>
using System;
using NUnit.Framework;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class M5LifecycleTests
    {
        [Test]
        public void ProtocolConstantsMatchServer()
        {
            Assert.That(MessageIds.LeaveRoom, Is.EqualTo(202));
            Assert.That(MessageIds.PlayerLeft, Is.EqualTo(902));
        }

        [Test]
        public void PlayerLeftKeepsLoginAndReturnsSurvivorToLobby()
        {
            NetworkSession session = CreatePlayingSession();

            session.ApplyPlayerLeft(new PlayerLeftDto
            {
                roomId = "R00001",
                leftUid = "guest-b",
                reason = "tcp_closed"
            });

            Assert.That(session.State, Is.EqualTo(OnlineSessionState.Lobby));
            Assert.That(session.Uid, Is.EqualTo("guest-a"));
            Assert.That(session.DisplayName, Is.EqualTo("PlayerA"));
            Assert.That(session.RoomId, Is.Null);
            Assert.That(session.LevelId, Is.Zero);
            Assert.That(session.Role, Is.Null);
            Assert.That(session.Revision, Is.EqualTo(-1));
            Assert.That(session.Snapshot, Is.Null);
        }

        [Test]
        public void DisconnectClearsLoginAndRoomState()
        {
            NetworkSession session = CreatePlayingSession();

            session.MarkDisconnected("测试 socket 异常");

            Assert.That(session.State, Is.EqualTo(OnlineSessionState.Disconnected));
            Assert.That(session.Uid, Is.Null);
            Assert.That(session.DisplayName, Is.Null);
            Assert.That(session.RoomId, Is.Null);
            Assert.That(session.LevelId, Is.Zero);
            Assert.That(session.Role, Is.Null);
            Assert.That(session.Revision, Is.EqualTo(-1));
            Assert.That(session.Snapshot, Is.Null);
        }

        [Test]
        public void PlayerLeftRejectsNotificationForLocalUid()
        {
            NetworkSession session = CreatePlayingSession();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    session.ApplyPlayerLeft(new PlayerLeftDto
                    {
                        roomId = "R00001",
                        leftUid = "guest-a",
                        reason = "client_leave"
                    }));

            StringAssert.Contains("不应通知离开者本人", exception.Message);
        }

        private static NetworkSession CreatePlayingSession()
        {
            var session = new NetworkSession();
            session.MarkConnected();
            session.BeginAuthentication();
            session.CompleteAuthentication("guest-a", "PlayerA");
            session.EnterWaitingRoom("R00001", 1, "Outer", 0);
            session.ApplySnapshot(new RoomSnapshotDto
            {
                roomId = "R00001",
                phase = "playing",
                levelId = 1,
                revision = 1,
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
                },
                level1 = new Level1StateDto()
            });
            session.MarkRoomReady(new RoomReadyDto
            {
                roomId = "R00001",
                levelId = 1,
                role = "Outer",
                revision = 1
            });
            session.MarkPlaying();
            return session;
        }
    }
}
