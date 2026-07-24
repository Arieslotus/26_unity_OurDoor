/// <summary>
/// 实现功能：验证 M7 任意关卡匹配、身份偏好约束及新增错误码。
/// </summary>
using System;
using NUnit.Framework;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class M7RoleAndAnyMatchTests
    {
        [Test]
        public void AnyLevelAcceptsServerSelectedLevelOne()
        {
            NetworkSession session = CreateLobbySession();
            session.BeginMatching(0, RolePreferences.Any);
            session.EnterMatchedRoom("R00007", 1, RolePreferences.Outer, 1);

            Assert.That(session.State, Is.EqualTo(OnlineSessionState.WaitingRoom));
            Assert.That(session.LevelId, Is.EqualTo(1));
            Assert.That(session.Role, Is.EqualTo(RolePreferences.Outer));
            Assert.That(session.MatchingRolePreference, Is.Null);
        }

        [Test]
        public void FixedOuterPreferenceRejectsInnerResult()
        {
            NetworkSession session = CreateLobbySession();
            session.BeginMatching(2, RolePreferences.Outer);

            Assert.Throws<InvalidOperationException>(() =>
                session.EnterMatchedRoom("R00007", 2, RolePreferences.Inner, 1));
            Assert.That(session.State, Is.EqualTo(OnlineSessionState.Matching));
            Assert.That(
                session.MatchingRolePreference,
                Is.EqualTo(RolePreferences.Outer));
        }

        [Test]
        public void CancelClearsAnyLevelAndRolePreference()
        {
            NetworkSession session = CreateLobbySession();
            session.BeginMatching(0, RolePreferences.Inner);
            session.CancelMatching();

            Assert.That(session.State, Is.EqualTo(OnlineSessionState.Lobby));
            Assert.That(session.MatchingLevelId, Is.Zero);
            Assert.That(session.MatchingRolePreference, Is.Null);
        }

        [Test]
        public void InvalidRolePreferenceIsRejected()
        {
            NetworkSession session = CreateLobbySession();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.BeginMatching(1, "Unknown"));
            Assert.That(
                ServerErrorCodes.InvalidRolePreference,
                Is.EqualTo(4003));
        }

        private static NetworkSession CreateLobbySession()
        {
            var session = new NetworkSession();
            session.MarkConnected();
            session.BeginAuthentication();
            session.CompleteAuthentication("guest-m7", "PlayerM7");
            return session;
        }
    }
}
