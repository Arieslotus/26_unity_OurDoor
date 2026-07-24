/// <summary>
/// 实现功能：验证 M4 协议常量和房间内原子切换关卡的会话状态迁移。
/// </summary>
using NUnit.Framework;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class M4LevelFlowTests
    {
        [Test]
        public void ProtocolConstantsMatchServer()
        {
            Assert.That(MessageIds.ReadyNextLevel, Is.EqualTo(301));
            Assert.That(MessageIds.LevelChanged, Is.EqualTo(904));
            Assert.That(ServerErrorCodes.LevelNotComplete, Is.EqualTo(3007));
            Assert.That(ServerErrorCodes.NoNextLevel, Is.EqualTo(3008));
        }

        [Test]
        public void LevelChangedAtomicallyMovesSessionToLoadingNextLevel()
        {
            NetworkSession session = CreatePlayingLevelOneSession();
            session.ApplyLevelChanged(new LevelChangedDto
            {
                roomId = "R00001",
                fromLevelId = 1,
                toLevelId = 2,
                role = "Outer",
                revision = 5,
                snapshot = CreateSnapshot(2, 5)
            });

            Assert.That(session.State, Is.EqualTo(OnlineSessionState.LoadingLevel));
            Assert.That(session.LevelId, Is.EqualTo(2));
            Assert.That(session.Revision, Is.EqualTo(5));
            Assert.That(session.Snapshot.level2, Is.Not.Null);

            session.MarkPlaying();
            Assert.That(session.State, Is.EqualTo(OnlineSessionState.Playing));
        }

        private static NetworkSession CreatePlayingLevelOneSession()
        {
            var session = new NetworkSession();
            session.MarkConnected();
            session.BeginAuthentication();
            session.CompleteAuthentication("guest-a", "PlayerA");
            session.EnterWaitingRoom("R00001", 1, "Outer", 0);
            session.ApplySnapshot(CreateSnapshot(1, 1));
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

        private static RoomSnapshotDto CreateSnapshot(int levelId, int revision)
        {
            return new RoomSnapshotDto
            {
                roomId = "R00001",
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
                },
                level1 = levelId == 1 ? new Level1StateDto() : null,
                level2 = levelId == 2 ? new Level2StateDto() : null,
                level3 = levelId == 3 ? new Level3StateDto() : null
            };
        }
    }
}
