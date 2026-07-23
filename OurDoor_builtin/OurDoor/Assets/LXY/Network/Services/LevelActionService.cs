/// <summary>
/// 实现功能：串行提交带幂等 ID 的关卡操作，并严格校验联网会话与服务端响应。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;

namespace OurDoor.LXY.Networking.Services
{
    public sealed class LevelActionService : IDisposable
    {
        private readonly NetworkManager _network;
        private readonly NetworkSession _session;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private long _nextActionSequence;
        private bool _disposed;

        public LevelActionService(NetworkManager network, NetworkSession session)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void ValidateSubmission(
            int levelId,
            string action,
            string clientActionId = null)
        {
            ThrowIfDisposed();
            if (_session.State != OnlineSessionState.Playing)
            {
                throw new InvalidOperationException(
                    $"[关卡操作服务] 提交操作要求状态 Playing，" +
                    $"当前={_session.State}, action={action ?? "null"}。");
            }
            if (string.IsNullOrWhiteSpace(_session.RoomId))
                throw new InvalidOperationException("[关卡操作服务] 当前会话缺少 roomId。");
            if (levelId != _session.LevelId)
            {
                throw new InvalidOperationException(
                    $"[关卡操作服务] 操作关卡与房间不一致，" +
                    $"操作={levelId}, 房间={_session.LevelId}。");
            }
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("action 不能为空。", nameof(action));
            if (clientActionId != null && string.IsNullOrWhiteSpace(clientActionId))
            {
                throw new ArgumentException(
                    "显式传入的 clientActionId 不能为空白。",
                    nameof(clientActionId));
            }
        }

        public async Task<LevelActionResponse> SubmitAsync(
            int levelId,
            string action,
            bool boolValue,
            string clientActionId = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSubmission(levelId, action, clientActionId);
            string resolvedActionId =
                clientActionId ?? CreateClientActionId();

            await _sendGate.WaitAsync(cancellationToken);
            try
            {
                ValidateSubmission(levelId, action, resolvedActionId);
                var response =
                    await _network.SendRequestAsync<
                        LevelActionRequest,
                        LevelActionResponse>(
                        MessageIds.LevelAction,
                        new LevelActionRequest
                        {
                            roomId = _session.RoomId,
                            levelId = levelId,
                            action = action,
                            boolValue = boolValue,
                            clientActionId = resolvedActionId
                        },
                        cancellationToken);

                if (response.code != ServerErrorCodes.Success)
                {
                    throw new ServerRequestException(
                        MessageIds.LevelAction,
                        response.code,
                        response.message);
                }
                if (!string.Equals(
                        response.clientActionId,
                        resolvedActionId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"[关卡操作服务] 响应 clientActionId 不一致，" +
                        $"请求={resolvedActionId}, 响应={response.clientActionId ?? "null"}。");
                }
                if (response.revision < 0)
                {
                    throw new InvalidOperationException(
                        $"[关卡操作服务] 服务端返回非法 revision={response.revision}，" +
                        $"clientActionId={resolvedActionId}。");
                }
                if (response.duplicate && response.changed)
                {
                    throw new InvalidOperationException(
                        $"[关卡操作服务] 响应同时标记 duplicate 和 changed，" +
                        $"clientActionId={resolvedActionId}, revision={response.revision}。");
                }

                return response;
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        private string CreateClientActionId()
        {
            if (string.IsNullOrWhiteSpace(_session.Uid))
                throw new InvalidOperationException("[关卡操作服务] 当前会话缺少 uid。");

            long sequence = Interlocked.Increment(ref _nextActionSequence);
            return $"{_session.Uid}-{sequence:D8}";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LevelActionService));
        }
    }
}
