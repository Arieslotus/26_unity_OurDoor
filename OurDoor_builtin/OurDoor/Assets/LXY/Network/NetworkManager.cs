using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Core;
using OurDoor.LXY.Networking.Protocol;
using UnityEngine;

namespace OurDoor.LXY.Networking
{
    public sealed class NetworkManager : MonoBehaviour
    {
        private sealed class PendingRequest
        {
            public readonly ushort MessageId;
            public readonly TaskCompletionSource<NetworkEnvelope> Completion;

            public PendingRequest(ushort messageId)
            {
                MessageId = messageId;
                Completion = new TaskCompletionSource<NetworkEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public static NetworkManager Instance { get; private set; }

        [SerializeField] private NetworkConfig config = new NetworkConfig();

        private readonly Dictionary<uint, PendingRequest> _pendingRequests = new Dictionary<uint, PendingRequest>();
        private readonly object _pendingLock = new object();
        private readonly object _sessionLock = new object();
        private NetworkClient _client;
        private uint _nextSession;
        private bool _shuttingDown;

        public event Action<NetworkEnvelope> PushReceived;
        public event Action<Exception> ConnectionLost;

        public bool IsConnected => _client != null && _client.IsConnected;
        public NetworkConfig Config => config;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            MainThreadDispatcher.EnsureExists();
            CreateClient();
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_client == null)
                CreateClient();
            await _client.ConnectAsync(config.Host, config.Port, config.ConnectTimeout, cancellationToken);
        }

        public void Disconnect()
        {
            _client?.Close();
            FailAllPending(new OperationCanceledException("Network connection closed."));
        }

        public async Task<HeartbeatMessage> HeartbeatAsync(int sequence, CancellationToken cancellationToken = default)
        {
            var request = new HeartbeatMessage
            {
                sequence = sequence,
                clientTimeUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return await SendRequestAsync<HeartbeatMessage, HeartbeatMessage>(MessageIds.Heartbeat, request, cancellationToken);
        }

        public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
            ushort messageId,
            TRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Network client is not connected.");

            var session = NextSession();
            var pending = new PendingRequest(messageId);
            lock (_pendingLock)
                _pendingRequests.Add(session, pending);

            try
            {
                var json = ReferenceEquals(request, null) ? string.Empty : JsonUtility.ToJson(request);
                var envelope = new NetworkEnvelope(messageId, session, MessageType.Request, json);
                await _client.SendPayloadAsync(ProtocolCodec.Encode(envelope), cancellationToken);

                var timeoutTask = Task.Delay(config.RequestTimeout, cancellationToken);
                var completed = await Task.WhenAny(pending.Completion.Task, timeoutTask);
                if (completed != pending.Completion.Task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"Request {messageId}/{session} timed out after {config.RequestTimeout.TotalSeconds:0.##} seconds.");
                }

                var response = await pending.Completion.Task;
                if (typeof(TResponse) == typeof(string))
                    return (TResponse)(object)response.JsonBody;
                if (string.IsNullOrEmpty(response.JsonBody))
                    return default;
                return JsonUtility.FromJson<TResponse>(response.JsonBody);
            }
            finally
            {
                lock (_pendingLock)
                    _pendingRequests.Remove(session);
            }
        }

        private void CreateClient()
        {
            _client?.Dispose();
            _client = new NetworkClient(config.MaximumPayloadLength, ProtocolCodec.HeaderSize);
            _client.PayloadReceived += OnPayloadReceived;
            _client.Disconnected += OnDisconnected;
        }

        private void OnPayloadReceived(byte[] payload)
        {
            MainThreadDispatcher.Post(() => HandlePayload(payload));
        }

        private void HandlePayload(byte[] payload)
        {
            NetworkEnvelope envelope;
            try
            {
                envelope = ProtocolCodec.Decode(payload);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LXY Network] Invalid payload: {exception.Message}");
                Disconnect();
                return;
            }

            if (envelope.MessageType == MessageType.Response)
            {
                PendingRequest pending;
                lock (_pendingLock)
                    _pendingRequests.TryGetValue(envelope.Session, out pending);

                if (pending == null)
                {
                    Debug.LogWarning($"[LXY Network] Unknown or expired response session {envelope.Session}.");
                    return;
                }

                if (pending.MessageId != envelope.MessageId)
                {
                    pending.Completion.TrySetException(new InvalidOperationException(
                        $"Response message ID {envelope.MessageId} does not match request {pending.MessageId}."));
                    return;
                }

                pending.Completion.TrySetResult(envelope);
                return;
            }

            if (envelope.MessageType == MessageType.Push)
                PushReceived?.Invoke(envelope);
        }

        private void OnDisconnected(Exception exception)
        {
            MainThreadDispatcher.Post(() =>
            {
                FailAllPending(exception);
                if (!_shuttingDown)
                    ConnectionLost?.Invoke(exception);
            });
        }

        private uint NextSession()
        {
            lock (_sessionLock)
            {
                do
                {
                    _nextSession++;
                    if (_nextSession == 0)
                        _nextSession = 1;
                }
                while (HasPendingSession(_nextSession));
                return _nextSession;
            }
        }

        private bool HasPendingSession(uint session)
        {
            lock (_pendingLock)
                return _pendingRequests.ContainsKey(session);
        }

        private void FailAllPending(Exception exception)
        {
            PendingRequest[] pending;
            lock (_pendingLock)
            {
                pending = new PendingRequest[_pendingRequests.Count];
                _pendingRequests.Values.CopyTo(pending, 0);
                _pendingRequests.Clear();
            }

            foreach (var request in pending)
                request.Completion.TrySetException(exception);
        }

        private void OnApplicationQuit()
        {
            _shuttingDown = true;
            Disconnect();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;
            _shuttingDown = true;
            Disconnect();
            _client?.Dispose();
            _client = null;
            Instance = null;
        }
    }
}
