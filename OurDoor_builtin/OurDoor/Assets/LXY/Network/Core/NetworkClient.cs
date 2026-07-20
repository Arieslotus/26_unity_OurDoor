using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OurDoor.LXY.Networking.Core
{
    public sealed class NetworkClient : IDisposable
    {
        private readonly PacketFramer _framer;
        private readonly int _minimumPayloadLength;
        private readonly int _maximumPayloadLength;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _lifecycleLock = new object();

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _connectionCancellation;
        private Task _receiveTask;
        private int _disconnectRaised;

        public NetworkClient(int maximumPayloadLength, int minimumPayloadLength)
        {
            _minimumPayloadLength = minimumPayloadLength;
            _maximumPayloadLength = maximumPayloadLength;
            _framer = new PacketFramer(maximumPayloadLength, minimumPayloadLength);
        }

        public event Action<byte[]> PayloadReceived;
        public event Action<Exception> Disconnected;

        public bool IsConnected => _tcpClient != null && _tcpClient.Connected && _stream != null;

        public async Task ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0 || port > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            Close();

            var tcpClient = new TcpClient { NoDelay = true };
            var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            lock (_lifecycleLock)
            {
                _tcpClient = tcpClient;
                _connectionCancellation = connectionCancellation;
                _disconnectRaised = 0;
                _framer.Reset();
            }

            try
            {
                var connectTask = tcpClient.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(timeout, cancellationToken);
                var completed = await Task.WhenAny(connectTask, timeoutTask);
                if (completed != connectTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"Connection to {host}:{port} timed out after {timeout.TotalSeconds:0.##} seconds.");
                }

                await connectTask;
                cancellationToken.ThrowIfCancellationRequested();

                lock (_lifecycleLock)
                {
                    if (_tcpClient != tcpClient)
                        throw new OperationCanceledException("Connection was replaced or closed.");
                    _stream = tcpClient.GetStream();
                    _receiveTask = ReceiveLoopAsync(connectionCancellation.Token);
                }
            }
            catch
            {
                Close();
                throw;
            }
        }

        public async Task SendPayloadAsync(byte[] payload, CancellationToken cancellationToken)
        {
            var packet = PacketFramer.Frame(payload, _minimumPayloadLength, _maximumPayloadLength);
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                var stream = _stream;
                if (stream == null || _tcpClient == null || !_tcpClient.Connected)
                    throw new InvalidOperationException("TCP client is not connected.");

                await stream.WriteAsync(packet, 0, packet.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Close()
        {
            TcpClient tcpClient;
            CancellationTokenSource cancellation;

            lock (_lifecycleLock)
            {
                tcpClient = _tcpClient;
                cancellation = _connectionCancellation;
                _tcpClient = null;
                _stream = null;
                _connectionCancellation = null;
                _receiveTask = null;
                _framer.Reset();
            }

            try { cancellation?.Cancel(); } catch (ObjectDisposedException) { }
            try { tcpClient?.Close(); } catch (SocketException) { }
            cancellation?.Dispose();
        }

        public void Dispose()
        {
            Close();
            _sendLock.Dispose();
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var readBuffer = new byte[8192];
            Exception disconnectReason = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var stream = _stream;
                    if (stream == null)
                        break;

                    var read = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken);
                    if (read == 0)
                    {
                        disconnectReason = new EndOfStreamException("Remote endpoint closed the connection.");
                        break;
                    }

                    var payloads = _framer.Append(readBuffer, 0, read);
                    foreach (var payload in payloads)
                        PayloadReceived?.Invoke(payload);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                disconnectReason = exception;
            }

            if (disconnectReason != null && Interlocked.Exchange(ref _disconnectRaised, 1) == 0)
            {
                Close();
                Disconnected?.Invoke(disconnectReason);
            }
        }
    }
}
