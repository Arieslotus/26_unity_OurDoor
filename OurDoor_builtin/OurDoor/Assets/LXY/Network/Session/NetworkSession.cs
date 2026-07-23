/// <summary>
/// 实现功能：保存临时账号和房间会话，并严格校验状态迁移、房间一致性与快照 revision。
/// </summary>
using System;
using OurDoor.LXY.Networking.Protocol;

namespace OurDoor.LXY.Networking.Session
{
    public sealed class NetworkSession
    {
        public event Action Changed;
        public event Action<RoomSnapshotDto> SnapshotChanged;

        public OnlineSessionState State { get; private set; } = OnlineSessionState.Disconnected;
        public string Uid { get; private set; }
        public string DisplayName { get; private set; }
        public string RoomId { get; private set; }
        public int LevelId { get; private set; }
        public string Role { get; private set; }
        public int Revision { get; private set; } = -1;
        public RoomSnapshotDto Snapshot { get; private set; }

        public void MarkConnected()
        {
            RequireState(OnlineSessionState.Disconnected, "标记连接成功");
            State = OnlineSessionState.Connected;
            RaiseChanged();
        }

        public void BeginAuthentication()
        {
            RequireState(OnlineSessionState.Connected, "开始临时登录");
            State = OnlineSessionState.Authenticating;
            RaiseChanged();
        }

        public void AuthenticationFailed()
        {
            RequireState(OnlineSessionState.Authenticating, "处理登录失败");
            State = OnlineSessionState.Connected;
            RaiseChanged();
        }

        public void CompleteAuthentication(string uid, string displayName)
        {
            RequireState(OnlineSessionState.Authenticating, "完成临时登录");
            RequireText(uid, nameof(uid));
            RequireText(displayName, nameof(displayName));

            Uid = uid;
            DisplayName = displayName;
            State = OnlineSessionState.Lobby;
            RaiseChanged();
        }

        public void EnterWaitingRoom(
            string roomId,
            int levelId,
            string role,
            int revision)
        {
            RequireState(OnlineSessionState.Lobby, "进入等待房间");
            ValidateRoomIdentity(roomId, levelId, role, revision);

            RoomId = roomId;
            LevelId = levelId;
            Role = role;
            Revision = -1;
            Snapshot = null;
            State = OnlineSessionState.WaitingRoom;
            RaiseChanged();
        }

        public bool ApplySnapshot(RoomSnapshotDto snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (State != OnlineSessionState.WaitingRoom &&
                State != OnlineSessionState.LoadingLevel &&
                State != OnlineSessionState.Playing)
            {
                throw new InvalidOperationException(
                    $"[网络会话] 状态 {State} 不允许应用房间快照。");
            }

            ValidateSnapshot(snapshot);
            if (snapshot.revision <= Revision)
                return false;

            Revision = snapshot.revision;
            Snapshot = snapshot;
            SnapshotChanged?.Invoke(snapshot);
            RaiseChanged();
            return true;
        }

        public void MarkRoomReady(RoomReadyDto ready)
        {
            if (ready == null)
                throw new ArgumentNullException(nameof(ready));
            RequireState(OnlineSessionState.WaitingRoom, "处理 ROOM_READY");
            ValidateRoomIdentity(ready.roomId, ready.levelId, ready.role, ready.revision);

            if (!string.Equals(RoomId, ready.roomId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"[网络会话] ROOM_READY 房间不一致，本地={RoomId}, 服务端={ready.roomId}。");
            if (LevelId != ready.levelId)
                throw new InvalidOperationException(
                    $"[网络会话] ROOM_READY 关卡不一致，本地={LevelId}, 服务端={ready.levelId}。");
            if (!string.Equals(Role, ready.role, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"[网络会话] ROOM_READY 角色不一致，本地={Role}, 服务端={ready.role}。");
            if (Snapshot == null)
                throw new InvalidOperationException(
                    $"[网络会话] ROOM_READY 到达前没有收到初始快照，roomId={RoomId}。");
            if (Revision != ready.revision)
                throw new InvalidOperationException(
                    $"[网络会话] ROOM_READY revision 不一致，本地={Revision}, 服务端={ready.revision}。");

            State = OnlineSessionState.LoadingLevel;
            RaiseChanged();
        }

        public void MarkPlaying()
        {
            RequireState(OnlineSessionState.LoadingLevel, "标记进入关卡");
            State = OnlineSessionState.Playing;
            RaiseChanged();
        }

        private void ValidateSnapshot(RoomSnapshotDto snapshot)
        {
            RequireText(snapshot.roomId, nameof(snapshot.roomId));
            RequireText(snapshot.phase, nameof(snapshot.phase));
            if (!string.Equals(RoomId, snapshot.roomId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"[网络会话] 快照房间不一致，本地={RoomId}, 服务端={snapshot.roomId}。");
            if (LevelId != snapshot.levelId)
                throw new InvalidOperationException(
                    $"[网络会话] 快照关卡不一致，本地={LevelId}, 服务端={snapshot.levelId}。");
            if (snapshot.revision < 0)
                throw new InvalidOperationException(
                    $"[网络会话] 快照 revision 非法：{snapshot.revision}。");
            if (snapshot.players == null)
                throw new InvalidOperationException("[网络会话] 房间快照缺少 players。");
        }

        private static void ValidateRoomIdentity(
            string roomId,
            int levelId,
            string role,
            int revision)
        {
            RequireText(roomId, nameof(roomId));
            RequireText(role, nameof(role));
            if (levelId < 1 || levelId > 3)
                throw new ArgumentOutOfRangeException(
                    nameof(levelId),
                    $"[网络会话] levelId 必须是 1、2、3，当前值={levelId}。");
            if (role != "Outer" && role != "Inner")
                throw new ArgumentOutOfRangeException(
                    nameof(role),
                    $"[网络会话] 未知角色：{role}。");
            if (revision < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(revision),
                    $"[网络会话] revision 不能小于 0，当前值={revision}。");
        }

        private void RequireState(OnlineSessionState expected, string operation)
        {
            if (State != expected)
            {
                throw new InvalidOperationException(
                    $"[网络会话] {operation}要求状态 {expected}，当前状态={State}。");
            }
        }

        private static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"[网络会话] {parameterName} 不能为空。", parameterName);
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
