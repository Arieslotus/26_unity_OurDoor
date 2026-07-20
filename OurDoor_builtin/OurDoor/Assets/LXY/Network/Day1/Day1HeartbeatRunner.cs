using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OurDoor.LXY.Networking.Day1
{
    public sealed class Day1HeartbeatRunner : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 8888;
        [SerializeField, Min(1)] private int heartbeatCount = 20;

        private CancellationTokenSource _cancellation;

        public string Status { get; private set; } = "Idle";
        public int SuccessfulHeartbeats { get; private set; }

        private async void Start()
        {
            if (runOnStart)
                await RunAsync();
        }

        [ContextMenu("Run Day 1 Heartbeat Verification")]
        public async void RunFromContextMenu()
        {
            await RunAsync();
        }

        public async Task RunAsync()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            SuccessfulHeartbeats = 0;

            try
            {
                var manager = NetworkManager.Instance;
                if (manager == null)
                    manager = gameObject.AddComponent<NetworkManager>();

                manager.Config.SetEndpoint(host, port);
                Status = $"Connecting to {host}:{port}";
                Debug.Log($"[LXY Day1] {Status}");
                await manager.ConnectAsync(_cancellation.Token);

                for (var sequence = 1; sequence <= heartbeatCount; sequence++)
                {
                    var response = await manager.HeartbeatAsync(sequence, _cancellation.Token);
                    if (response == null || response.sequence != sequence)
                        throw new InvalidOperationException($"Heartbeat sequence mismatch: expected {sequence}, received {response?.sequence}.");

                    SuccessfulHeartbeats++;
                    Status = $"Heartbeat {SuccessfulHeartbeats}/{heartbeatCount}";
                    Debug.Log($"[LXY Day1] {Status}");
                }

                Status = $"PASS: {SuccessfulHeartbeats}/{heartbeatCount} heartbeats succeeded";
                Debug.Log($"[LXY Day1] {Status}");
            }
            catch (OperationCanceledException)
            {
                Status = "Canceled";
                Debug.LogWarning("[LXY Day1] Verification canceled.");
            }
            catch (Exception exception)
            {
                Status = $"FAIL after {SuccessfulHeartbeats}/{heartbeatCount}: {exception.Message}";
                Debug.LogException(exception);
            }
        }

        private void OnDestroy()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }
}
