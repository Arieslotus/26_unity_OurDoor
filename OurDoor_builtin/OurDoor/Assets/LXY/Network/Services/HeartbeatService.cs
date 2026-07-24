/// <summary>
/// 实现功能：登录后持续发送生产心跳，并把超时或协议错误作为连接故障上报。
/// </summary>
using System;
using System.Threading;
using System.Threading.Tasks;
using OurDoor.LXY.Networking.Core;

namespace OurDoor.LXY.Networking.Services
{
    public sealed class HeartbeatService : IDisposable
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

        private readonly NetworkManager network;
        private CancellationTokenSource loopCancellation;
        private bool disposed;
        private int sequence;

        public event Action<Exception> Failed;

        public bool IsRunning =>
            loopCancellation != null &&
            !loopCancellation.IsCancellationRequested;

        public HeartbeatService(NetworkManager network)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public void Start(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (IsRunning)
                throw new InvalidOperationException("[心跳服务] 心跳循环已经运行。");
            if (!network.IsConnected)
                throw new InvalidOperationException("[心跳服务] 网络未连接，不能启动心跳。");

            loopCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sequence = 0;
            RunAsync(loopCancellation.Token);
        }

        public void Stop()
        {
            if (loopCancellation == null)
                return;

            loopCancellation.Cancel();
            loopCancellation.Dispose();
            loopCancellation = null;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Stop();
            Failed = null;
        }

        private async void RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int expectedSequence = checked(++sequence);
                    var response =
                        await network.HeartbeatAsync(
                            expectedSequence,
                            cancellationToken);
                    if (response == null)
                        throw new InvalidOperationException("[心跳服务] 服务端响应为空。");
                    if (response.sequence != expectedSequence)
                    {
                        throw new InvalidOperationException(
                            $"[心跳服务] sequence 不一致，" +
                            $"发送={expectedSequence}, 返回={response.sequence}。");
                    }

                    await Task.Delay(Interval, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                MainThreadDispatcher.Post(() => Failed?.Invoke(exception));
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(HeartbeatService));
        }
    }
}
