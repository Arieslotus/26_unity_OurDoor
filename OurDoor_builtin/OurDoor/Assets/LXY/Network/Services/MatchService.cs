/// <summary>
/// 实现功能：按关卡请求或取消匹配，消费 MATCH_FOUND 并驱动严格的匹配会话状态迁移。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;
using UnityEngine;

namespace OurDoor.LXY.Networking.Services
{
    public sealed class MatchService : IDisposable
    {
        private readonly NetworkManager network;
        private readonly NetworkSession session;
        private bool disposed;
        private bool operationPending;

        public event Action<MatchFoundDto> MatchFound;

        public MatchService(NetworkManager network, NetworkSession session)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            network.PushReceived += OnPushReceived;
        }

        public async Task<MatchRequestResponse> RequestMatchAsync(
            int levelId,
            string rolePreference,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateLevelId(levelId);
            RolePreferences.Validate(rolePreference, nameof(rolePreference));
            if (session.State != OnlineSessionState.Lobby)
            {
                throw new InvalidOperationException(
                    $"[匹配服务] 请求匹配要求状态 Lobby，当前={session.State}。");
            }
            BeginOperation("请求匹配");
            session.BeginMatching(levelId, rolePreference);
            try
            {
                MatchRequestResponse response =
                    await network.SendRequestAsync<
                        MatchRequest,
                        MatchRequestResponse>(
                        MessageIds.MatchRequest,
                        new MatchRequest
                        {
                            levelId = levelId,
                            rolePreference = rolePreference
                        },
                        cancellationToken);

                if (response.code != ServerErrorCodes.Success)
                {
                    if (session.State != OnlineSessionState.Matching)
                    {
                        throw new InvalidOperationException(
                            $"[匹配服务] 请求失败时会话已离开 Matching，" +
                            $"state={session.State}, code={response.code}。");
                    }
                    session.CancelMatching();
                    throw new ServerRequestException(
                        MessageIds.MatchRequest,
                        response.code,
                        response.message);
                }
                if (!string.Equals(
                        response.rolePreference,
                        rolePreference,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"[匹配服务] MATCH_REQUEST 响应身份偏好不一致，" +
                        $"请求={rolePreference}, 响应={response.rolePreference ?? "null"}。");
                }
                if (response.levelId != levelId)
                {
                    throw new InvalidOperationException(
                        $"[匹配服务] MATCH_REQUEST 响应关卡不一致，" +
                        $"请求={levelId}, 响应={response.levelId}。");
                }

                Debug.Log(
                    $"[匹配服务] 匹配请求已确认，levelId={levelId}, " +
                    $"queued={response.queued}, state={session.State}。");
                return response;
            }
            finally
            {
                operationPending = false;
            }
        }

        public async Task<MatchCancelResponse> CancelMatchAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (session.State != OnlineSessionState.Matching)
            {
                throw new InvalidOperationException(
                    $"[匹配服务] 取消匹配要求状态 Matching，当前={session.State}。");
            }
            BeginOperation("取消匹配");
            int requestedLevelId = session.MatchingLevelId;
            string requestedRolePreference = session.MatchingRolePreference;
            try
            {
                MatchCancelResponse response =
                    await network.SendRequestAsync<
                        MatchCancelRequest,
                        MatchCancelResponse>(
                        MessageIds.MatchCancel,
                        new MatchCancelRequest(),
                        cancellationToken);

                if (response.code != ServerErrorCodes.Success)
                {
                    throw new ServerRequestException(
                        MessageIds.MatchCancel,
                        response.code,
                        response.message);
                }
                if (!string.Equals(
                        response.rolePreference,
                        requestedRolePreference,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"[匹配服务] MATCH_CANCEL 响应身份偏好不一致，" +
                        $"请求={requestedRolePreference}, " +
                        $"响应={response.rolePreference ?? "null"}。");
                }
                if (!response.removed)
                    throw new InvalidOperationException("[匹配服务] 取消成功但服务端未移除队列项。");
                if (response.levelId != requestedLevelId)
                {
                    throw new InvalidOperationException(
                        $"[匹配服务] MATCH_CANCEL 响应关卡不一致，" +
                        $"请求={requestedLevelId}, 响应={response.levelId}。");
                }
                if (session.State != OnlineSessionState.Matching)
                {
                    throw new InvalidOperationException(
                        $"[匹配服务] 取消成功时会话已离开 Matching，" +
                        $"当前={session.State}。");
                }

                session.CancelMatching();
                Debug.Log($"[匹配服务] 已取消匹配，levelId={requestedLevelId}。");
                return response;
            }
            finally
            {
                operationPending = false;
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
            if (envelope.MessageId != MessageIds.MatchFound)
                return;
            if (string.IsNullOrWhiteSpace(envelope.JsonBody))
                throw new InvalidOperationException("[匹配服务] MATCH_FOUND 缺少 JSON。");

            MatchFoundDto found =
                JsonUtility.FromJson<MatchFoundDto>(envelope.JsonBody);
            if (found == null)
            {
                throw new InvalidOperationException(
                    $"[匹配服务] MATCH_FOUND JSON 无法解析，JSON={envelope.JsonBody}");
            }

            session.EnterMatchedRoom(
                found.roomId,
                found.levelId,
                found.role,
                found.revision);
            Debug.Log(
                $"[匹配服务] 匹配成功，roomId={found.roomId}, " +
                $"levelId={found.levelId}, role={found.role}, revision={found.revision}。");
            MatchFound?.Invoke(found);
        }

        private void BeginOperation(string operationName)
        {
            if (operationPending)
            {
                throw new InvalidOperationException(
                    $"[匹配服务] 已有操作正在执行，不能开始{operationName}。");
            }
            operationPending = true;
        }

        private static void ValidateLevelId(int levelId)
        {
            if (levelId < 0 || levelId > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelId),
                    $"[匹配服务] levelId 必须是 0、1、2、3，当前={levelId}。");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MatchService));
        }
    }
}
