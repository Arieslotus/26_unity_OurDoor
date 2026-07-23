/// <summary>
/// 实现功能：执行临时账号登录，并将请求结果写入严格状态化的网络会话。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Protocol;
using OurDoor.LXY.Networking.Session;

namespace OurDoor.LXY.Networking.Services
{
    public sealed class AuthService
    {
        private readonly NetworkManager _network;
        private readonly NetworkSession _session;

        public AuthService(NetworkManager network, NetworkSession session)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task LoginGuestAsync(
            string guestId,
            string displayName,
            string clientVersion,
            CancellationToken cancellationToken = default)
        {
            RequireText(guestId, nameof(guestId));
            RequireText(displayName, nameof(displayName));
            RequireText(clientVersion, nameof(clientVersion));

            _session.BeginAuthentication();
            try
            {
                var response =
                    await _network.SendRequestAsync<GuestLoginRequest, GuestLoginResponse>(
                        MessageIds.GuestLogin,
                        new GuestLoginRequest
                        {
                            guestId = guestId,
                            displayName = displayName,
                            clientVersion = clientVersion
                        },
                        cancellationToken);

                if (response.code != ServerErrorCodes.Success)
                {
                    throw new ServerRequestException(
                        MessageIds.GuestLogin,
                        response.code,
                        response.message);
                }

                _session.CompleteAuthentication(response.uid, response.displayName);
            }
            catch
            {
                if (_session.State == OnlineSessionState.Authenticating)
                    _session.AuthenticationFailed();
                throw;
            }
        }

        private static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{parameterName} 不能为空。", parameterName);
        }
    }
}
