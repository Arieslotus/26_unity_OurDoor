/// <summary>
/// 实现功能：创建或加入双人房间，消费初始快照与 ROOM_READY 推送并更新网络会话。
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

namespace OurDoor.LXY.Networking.Services
{
    public sealed class RoomService : IDisposable
    {
        private readonly NetworkManager _network;
        private readonly NetworkSession _session;
        private readonly List<NetworkEnvelope> _deferredRoomPushes =
            new List<NetworkEnvelope>();
        private bool _disposed;
        private bool _roomRequestPending;

        public event Action<RoomReadyDto> RoomReady;

        public RoomService(NetworkManager network, NetworkSession session)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _network.PushReceived += OnPushReceived;
        }

        public async Task<string> CreateRoomAsync(
            int levelId,
            CancellationToken cancellationToken = default)
        {
            ValidateLevelId(levelId);
            RequireLobby();
            BeginRoomRequest();
            try
            {
                var response =
                    await _network.SendRequestAsync<CreateRoomRequest, CreateRoomResponse>(
                        MessageIds.CreateRoom,
                        new CreateRoomRequest { levelId = levelId },
                        cancellationToken);

                if (response.code != ServerErrorCodes.Success)
                {
                    throw new ServerRequestException(
                        MessageIds.CreateRoom,
                        response.code,
                        response.message);
                }

                _session.EnterWaitingRoom(
                    response.roomId,
                    response.levelId,
                    response.role,
                    response.revision);
                CompleteRoomRequest();
                return response.roomId;
            }
            catch (Exception exception)
            {
                CancelRoomRequest(exception);
                throw;
            }
        }

        public async Task JoinRoomAsync(
            string roomId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                throw new ArgumentException("roomId 不能为空。", nameof(roomId));
            RequireLobby();
            BeginRoomRequest();
            try
            {
                var response =
                    await _network.SendRequestAsync<JoinRoomRequest, JoinRoomResponse>(
                        MessageIds.JoinRoom,
                        new JoinRoomRequest
                        {
                            roomId = roomId.Trim().ToUpperInvariant()
                        },
                        cancellationToken);

                if (response.code != ServerErrorCodes.Success)
                {
                    throw new ServerRequestException(
                        MessageIds.JoinRoom,
                        response.code,
                        response.message);
                }

                _session.EnterWaitingRoom(
                    response.roomId,
                    response.levelId,
                    response.role,
                    response.revision);
                CompleteRoomRequest();
            }
            catch (Exception exception)
            {
                CancelRoomRequest(exception);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _network.PushReceived -= OnPushReceived;
        }

        private void OnPushReceived(NetworkEnvelope envelope)
        {
            if (_roomRequestPending &&
                _session.State == OnlineSessionState.Lobby &&
                (envelope.MessageId == MessageIds.RoomSnapshot ||
                 envelope.MessageId == MessageIds.RoomReady))
            {
                _deferredRoomPushes.Add(envelope);
                return;
            }

            HandleRoomPush(envelope);
        }

        private void HandleRoomPush(NetworkEnvelope envelope)
        {
            if (envelope.MessageId == MessageIds.RoomSnapshot)
            {
                var snapshot = ParsePush<RoomSnapshotDto>(envelope, "ROOM_SNAPSHOT");
                bool applied = _session.ApplySnapshot(snapshot);
                if (applied)
                {
                    Debug.Log(
                        $"[房间服务] 已应用快照，roomId={snapshot.roomId}, " +
                        $"levelId={snapshot.levelId}, revision={snapshot.revision}。");
                }
                return;
            }

            if (envelope.MessageId == MessageIds.RoomReady)
            {
                var ready = ParsePush<RoomReadyDto>(envelope, "ROOM_READY");
                _session.MarkRoomReady(ready);
                RoomReady?.Invoke(ready);
                return;
            }

            if (envelope.MessageId == MessageIds.ServerError)
            {
                var error = ParsePush<ServerErrorDto>(envelope, "SERVER_ERROR");
                throw new ServerRequestException(
                    MessageIds.ServerError,
                    error.code,
                    error.message);
            }
        }

        private void BeginRoomRequest()
        {
            if (_roomRequestPending)
                throw new InvalidOperationException("[房间服务] 已有房间请求正在处理。");
            if (_deferredRoomPushes.Count != 0)
                throw new InvalidOperationException("[房间服务] 请求开始前存在未处理的房间推送。");

            _roomRequestPending = true;
        }

        private void CompleteRoomRequest()
        {
            if (!_roomRequestPending)
                throw new InvalidOperationException("[房间服务] 没有可完成的房间请求。");

            _roomRequestPending = false;
            if (_deferredRoomPushes.Count == 0)
                return;

            var deferred = _deferredRoomPushes.ToArray();
            _deferredRoomPushes.Clear();
            foreach (var envelope in deferred)
                HandleRoomPush(envelope);
        }

        private void CancelRoomRequest(Exception requestException)
        {
            int deferredCount = _deferredRoomPushes.Count;
            _roomRequestPending = false;
            _deferredRoomPushes.Clear();
            if (deferredCount > 0)
            {
                throw new InvalidOperationException(
                    $"[房间服务] 房间请求失败时收到了 {deferredCount} 条房间推送，" +
                    "服务端响应与推送状态不一致。",
                    requestException);
            }
        }

        private static T ParsePush<T>(NetworkEnvelope envelope, string pushName)
        {
            if (string.IsNullOrWhiteSpace(envelope.JsonBody))
                throw new InvalidOperationException($"[房间服务] {pushName} 缺少 JSON 数据。");

            var result = JsonUtility.FromJson<T>(envelope.JsonBody);
            if (ReferenceEquals(result, null))
            {
                throw new InvalidOperationException(
                    $"[房间服务] {pushName} JSON 无法解析为 {typeof(T).Name}，" +
                    $"JSON={envelope.JsonBody}");
            }
            return result;
        }

        private void RequireLobby()
        {
            if (_session.State != OnlineSessionState.Lobby)
            {
                throw new InvalidOperationException(
                    $"[房间服务] 创建或加入房间要求状态 Lobby，当前状态={_session.State}。");
            }
        }

        private static void ValidateLevelId(int levelId)
        {
            if (levelId < 1 || levelId > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelId),
                    $"levelId 必须是 1、2、3，当前值={levelId}。");
            }
        }
    }
}
