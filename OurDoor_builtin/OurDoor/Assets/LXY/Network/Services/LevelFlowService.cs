/// <summary>
/// 实现功能：提交本端换关准备，并消费包含下一关完整快照的 LEVEL_CHANGED 推送。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

namespace OurDoor.LXY.Networking.Services
{
    public sealed class LevelFlowService : IDisposable
    {
        private readonly NetworkManager network;
        private readonly NetworkSession session;
        private bool disposed;

        public event Action<LevelChangedDto> LevelChanged;

        public LevelFlowService(NetworkManager network, NetworkSession session)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            network.PushReceived += OnPushReceived;
        }

        public async Task<ReadyNextLevelResponse> ReadyForNextLevelAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (session.State != OnlineSessionState.Playing)
            {
                throw new InvalidOperationException(
                    $"[换关服务] 提交换关准备要求状态 Playing，当前={session.State}。");
            }
            if (session.LevelId < 1 || session.LevelId >= 3)
            {
                throw new InvalidOperationException(
                    $"[换关服务] 当前关卡没有可进入的下一关，levelId={session.LevelId}。");
            }
            if (string.IsNullOrWhiteSpace(session.RoomId))
                throw new InvalidOperationException("[换关服务] 当前会话缺少 roomId。");

            string requestedRoomId = session.RoomId;
            int requestedLevelId = session.LevelId;
            var response =
                await network.SendRequestAsync<
                    ReadyNextLevelRequest,
                    ReadyNextLevelResponse>(
                    MessageIds.ReadyNextLevel,
                    new ReadyNextLevelRequest
                    {
                        roomId = requestedRoomId,
                        levelId = requestedLevelId
                    },
                    cancellationToken);

            if (response.code != ServerErrorCodes.Success)
            {
                throw new ServerRequestException(
                    MessageIds.ReadyNextLevel,
                    response.code,
                    response.message);
            }
            if (!string.Equals(
                    response.roomId,
                    requestedRoomId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"[换关服务] 响应 roomId 不一致，" +
                    $"请求={requestedRoomId}, 响应={response.roomId ?? "null"}。");
            }
            if (response.levelId != requestedLevelId)
            {
                throw new InvalidOperationException(
                    $"[换关服务] 响应 levelId 不一致，" +
                    $"请求={requestedLevelId}, 响应={response.levelId}。");
            }
            if (response.readyCount < 1 || response.readyCount > 2)
            {
                throw new InvalidOperationException(
                    $"[换关服务] 响应 readyCount 非法：{response.readyCount}。");
            }
            if (response.revision < 0)
            {
                throw new InvalidOperationException(
                    $"[换关服务] 响应 revision 非法：{response.revision}。");
            }

            return response;
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
            if (envelope.MessageId != MessageIds.LevelChanged)
                return;
            if (string.IsNullOrWhiteSpace(envelope.JsonBody))
                throw new InvalidOperationException("[换关服务] LEVEL_CHANGED 缺少 JSON。");

            var changed = JsonUtility.FromJson<LevelChangedDto>(envelope.JsonBody);
            if (changed == null)
            {
                throw new InvalidOperationException(
                    $"[换关服务] LEVEL_CHANGED JSON 无法解析，JSON={envelope.JsonBody}");
            }

            session.ApplyLevelChanged(changed);
            LevelChanged?.Invoke(changed);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(LevelFlowService));
        }
    }
}
