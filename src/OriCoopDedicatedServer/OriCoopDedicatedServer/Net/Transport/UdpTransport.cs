using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OriCoopDedicatedServer.Net.Diagnostics;

namespace OriCoopDedicatedServer.Net.Transport
{
    /// <summary>
    /// Datagrama UDP recebido com origem, para dispatch pelo host (D-04).
    /// </summary>
    public sealed class ReceivedDatagram
    {
        public byte[] Data = Array.Empty<byte>();
        public IPEndPoint Remote = new IPEndPoint(IPAddress.Loopback, 0);
    }

    /// <summary>
    /// Transporte UDP moderno (D-01/D-04): single loop ReceiveAsync com
    /// CancellationToken enfileirando em Channel bounded (1024, DropOldest)
    /// e SendAsync direto. Sem BeginReceive/BeginSend.
    /// </summary>
    public sealed class UdpTransport : IDisposable
    {
        private readonly ILogger _log;
        private readonly Channel<ReceivedDatagram> _channel;
        private UdpClient _udp = null!;
        private Task _receiveTask = null!;
        private bool _disposed;
        private DateTime _lastSockWarnUtc = DateTime.MinValue;

        public UdpTransport(ILogger log)
        {
            _log = log ?? throw new ArgumentNullException("log");
            _channel = Channel.CreateBounded<ReceivedDatagram>(new BoundedChannelOptions(1024)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            });
        }

        public ChannelReader<ReceivedDatagram> Reader
        {
            get { return _channel.Reader; }
        }

        public void Start(int port)
        {
            if (_udp != null)
            {
                throw new InvalidOperationException("Transporte ja iniciado.");
            }
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            try
            {
                // Windows: sem isso, ICMP Port Unreachable de um endpoint morto
                // (cliente fechado sem DISCONNECT) estoura ConnectionReset no
                // proximo ReceiveAsync em loop — era o flood de warnings.
                _udp.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception)
            {
            }
        }

        public Task RunReceiveLoopAsync(CancellationToken ct)
        {
            if (_udp == null)
            {
                throw new InvalidOperationException("Chame Start(port) antes do loop.");
            }
            if (_receiveTask != null)
            {
                return _receiveTask;
            }
            _receiveTask = ReceiveLoopAsync(ct);
            return _receiveTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    UdpReceiveResult result;
                    try
                    {
                        result = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException ex)
                    {
                        DateTime now = DateTime.UtcNow;
                        if ((now - _lastSockWarnUtc).TotalSeconds >= 5.0)
                        {
                            _lastSockWarnUtc = now;
                            _log.Log(ServerLogLevel.Warning, "UDP", "ReceiveAsync falhou (segue ouvindo): " + ex.SocketErrorCode);
                        }
                        continue;
                    }

                    var datagram = new ReceivedDatagram
                    {
                        Data = result.Buffer,
                        Remote = result.RemoteEndPoint,
                    };
                    // DropOldest: nunca bloqueia o socket; sob flood o mais antigo cai.
                    _channel.Writer.TryWrite(datagram);
                }
            }
            finally
            {
                _channel.Writer.TryComplete();
            }
        }

        public async Task<int> SendAsync(byte[] datagram, IPEndPoint remote, CancellationToken ct)
        {
            if (_udp == null)
            {
                throw new InvalidOperationException("Chame Start(port) antes de enviar.");
            }
            return await _udp.SendAsync(datagram, remote, ct).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            {
                if (_udp != null)
                {
                    _udp.Close();
                }
            }
            catch (Exception)
            {
            }
            _channel.Writer.TryComplete();
        }
    }
}
