/// <summary>
/// 实现功能：发送主动退出请求，并消费服务端唯一一次的对端离开通知。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

namespace OurDoor.LXY.Networking.Services
{
    public sealed class RoomLifecycleService : IDisposable
    {
        private readonly NetworkManager network;
        private readonly NetworkSession session;
        private bool disposed;
        private bool leavePending;

        public event Action<PlayerLeftDto> PlayerLeft;

        public RoomLifecycleService(
            NetworkManager network,
            NetworkSession session)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            network.PushReceived += OnPushReceived;
        }

        public async Task<LeaveRoomResponse> LeaveRoomAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (leavePending)
                throw new InvalidOperationException("[房间生命周期] 主动退出请求正在处理中。");
            if (session.State != OnlineSessionState.WaitingRoom &&
                session.State != OnlineSessionState.LoadingLevel &&
                session.State != OnlineSessionState.Playing)
            {
                throw new InvalidOperationException(
                    $"[房间生命周期] 当前状态不能主动退出房间，state={session.State}。");
            }
            if (string.IsNullOrWhiteSpace(session.RoomId))
                throw new InvalidOperationException("[房间生命周期] 当前会话缺少 roomId。");

            string requestedRoomId = session.RoomId;
            leavePending = true;
            try
            {
                LeaveRoomResponse response =
                    await network.SendRequestAsync<
                        LeaveRoomRequest,
                        LeaveRoomResponse>(
                        MessageIds.LeaveRoom,
                        new LeaveRoomRequest { roomId = requestedRoomId },
                        cancellationToken);

                if (response.code != ServerErrorCodes.Success)
                {
                    throw new ServerRequestException(
                        MessageIds.LeaveRoom,
                        response.code,
                        response.message);
                }
                if (!string.Equals(
                        response.roomId,
                        requestedRoomId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"[房间生命周期] LEAVE_ROOM 响应房间不一致，" +
                        $"请求={requestedRoomId}, 响应={response.roomId ?? "null"}。");
                }

                return response;
            }
            finally
            {
                leavePending = false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            network.PushReceived -= OnPushReceived;
        }

        private void OnPushReceived(NetworkEnvelope envelope)
        {
            if (envelope.MessageId != MessageIds.PlayerLeft)
                return;
            if (string.IsNullOrWhiteSpace(envelope.JsonBody))
                throw new InvalidOperationException("[房间生命周期] PLAYER_LEFT 缺少 JSON。");

            PlayerLeftDto playerLeft =
                JsonUtility.FromJson<PlayerLeftDto>(envelope.JsonBody);
            if (playerLeft == null)
            {
                throw new InvalidOperationException(
                    $"[房间生命周期] PLAYER_LEFT JSON 无法解析，" +
                    $"JSON={envelope.JsonBody}");
            }

            session.ApplyPlayerLeft(playerLeft);
            PlayerLeft?.Invoke(playerLeft);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(RoomLifecycleService));
        }
    }
}
