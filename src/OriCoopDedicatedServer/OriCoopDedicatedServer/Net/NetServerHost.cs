using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OriCoop;
using OriCoopDedicatedServer.Net.Diagnostics;
using OriCoopDedicatedServer.Net.Game;
using OriCoopDedicatedServer.Net.Reliability;
using OriCoopDedicatedServer.Net.Session;
using OriCoopDedicatedServer.Net.Transport;

namespace OriCoopDedicatedServer.Net
{
    /// <summary>
    /// Orquestracao do novo core (D-13): receive (Channel) -> dispatch
    /// (SessionManager + relay) -> send (UdpTransport). Sem estatico global.
    /// Pacotes originados no servidor usam clientId 0; relay reemite bytes
    /// originais preservando clientId + seq do remetente. Criticos (D-10)
    /// usam ACK + retry via AckTracker (250 ms x3); PLAYER_STATE e Ping
    /// seguem unreliable sem pendencia.
    /// </summary>
    public sealed class NetServerHost
    {
        private const int ChatPacketId = -5;

        private readonly int _port;
        private readonly int _maxPlayers;
        private readonly ILogger _log;
        private readonly UdpTransport _transport;
        private readonly SessionManager _sessions;
        private readonly PlayerStateRelay _relay;
        private readonly AckTracker _acks = new AckTracker();
        private Timer _retryTimer = null!;
        private uint _serverSeq;

        public NetServerHost(int port, int maxPlayers, ILogger log)
        {
            _port = port;
            _maxPlayers = maxPlayers;
            _log = log ?? throw new ArgumentNullException("log");
            _transport = new UdpTransport(_log);
            _sessions = new SessionManager(maxPlayers, _log);
            _relay = new PlayerStateRelay();
        }

        public async Task RunAsync(CancellationToken ct)
        {
            _transport.Start(_port);
            LogBindAddresses();
            _log.Log(ServerLogLevel.Info, "NET2", "Novo core ouvindo na porta " + _port
                + " (max " + _maxPlayers + " jogadores). Envelope 0x4F43 v2.");
            // Timer unico de retry (D-10): reavalia pendencias a cada 50 ms.
            _retryTimer = new Timer(OnRetryTick, null, 50, 50);
            Task receiveTask = _transport.RunReceiveLoopAsync(ct);
            try
            {
                await foreach (ReceivedDatagram datagram in _transport.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    await DispatchAsync(datagram, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                try
                {
                    if (_retryTimer != null)
                    {
                        _retryTimer.Dispose();
                    }
                }
                catch (Exception)
                {
                }
                _transport.Dispose();
                try
                {
                    await receiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            _log.Log(ServerLogLevel.Info, "NET2", "Host encerrado.");
        }

        private async Task DispatchAsync(ReceivedDatagram datagram, CancellationToken ct)
        {
            NetEnvelope header;
            byte[] payload;
            string decodeError;
            if (!EnvelopeCodec.TryDecode(datagram.Data, out header, out payload, out decodeError))
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Drop de " + datagram.Remote + ": " + decodeError);
                await SendRejectAsync(datagram.Remote, decodeError, ct).ConfigureAwait(false);
                return;
            }

            // Todo datagrama Reliable recebe SysAck imediato antes do dispatch
            // (D-10); o remetente usa o ACK para cancelar os proprios retries.
            if ((header.Flags & NetProtocol.FlagReliable) != 0)
            {
                await SendSysAckAsync(datagram.Remote, header.Seq, ct).ConfigureAwait(false);
            }
            // ACK piggybacked no header (AckPresent + ackSeq) vale como SysAck.
            if ((header.Flags & NetProtocol.FlagAckPresent) != 0)
            {
                _acks.Complete(datagram.Remote, header.AckSeq);
            }

            switch (header.PacketId)
            {
                case NetProtocol.MsgHello:
                    await HandleHelloAsync(datagram.Remote, header, payload, ct).ConfigureAwait(false);
                    break;
                case NetProtocol.MsgConfirm:
                    await HandleConfirmAsync(datagram.Remote, header, ct).ConfigureAwait(false);
                    break;
                case NetProtocol.MsgPing:
                    await HandlePingAsync(datagram.Remote, header, payload, ct).ConfigureAwait(false);
                    break;
                case NetProtocol.MsgAck:
                    // SysAck 103: payload = uint32 LE com a seq confirmada.
                    if (payload != null && payload.Length >= 4)
                    {
                        uint ackedSeq = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
                        _acks.Complete(datagram.Remote, ackedSeq);
                    }
                    TouchSession(header);
                    break;
                default:
                    await HandleGamePacketAsync(datagram, header, ct).ConfigureAwait(false);
                    break;
            }
        }

        private async Task HandleHelloAsync(IPEndPoint remote, NetEnvelope header, byte[] payload, CancellationToken ct)
        {
            // clientId -1 restrito ao Hello (T-02-01); D-08 completo no plano 02-02.
            if (header.ClientId != NetProtocol.PreHandshakeId || header.Token != 0)
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Hello fora do formato de " + remote + " (clientId/token devem ser -1/0)");
                await SendRejectAsync(remote, "Hello deve usar clientId -1 e token 0", ct).ConfigureAwait(false);
                return;
            }
            Session.Session session;
            byte[] welcomePayload;
            string rejectReason;
            if (!_sessions.HandleHello(remote, payload, out session, out welcomePayload, out rejectReason))
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Hello recusado de " + remote + ": " + rejectReason);
                await SendRejectAsync(remote, rejectReason, ct).ConfigureAwait(false);
                return;
            }
            await SendSystemAsync(session.EndPoint, session.Id, NetProtocol.MsgWelcome, welcomePayload, ct).ConfigureAwait(false);
        }

        private async Task HandleConfirmAsync(IPEndPoint remote, NetEnvelope header, CancellationToken ct)
        {
            if (header.ClientId == NetProtocol.PreHandshakeId)
            {
                await SendRejectAsync(remote, "Confirm exige ID atribuido pelo Welcome", ct).ConfigureAwait(false);
                return;
            }
            Session.Session session;
            string rejectReason;
            if (!_sessions.HandleConfirm(header.ClientId, header.Token, out session, out rejectReason))
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Confirm recusado de " + remote + ": " + rejectReason);
                await SendRejectAsync(remote, rejectReason, ct).ConfigureAwait(false);
                return;
            }
            // Baseline do drop-old: seqs do remetente passam a valer a partir daqui.
            session.LastRecvSeq = header.Seq;
        }

        private async Task HandlePingAsync(IPEndPoint remote, NetEnvelope header, byte[] payload, CancellationToken ct)
        {
            TouchSession(header);
            // Pong = eco stateless do payload (sendTicks do cliente, D-12).
            await SendSystemAsync(remote, NetProtocol.ServerId, NetProtocol.MsgPong, payload, ct).ConfigureAwait(false);
        }

        private Task HandleGamePacketAsync(ReceivedDatagram datagram, NetEnvelope header, CancellationToken ct)
        {
            if (header.ClientId == NetProtocol.PreHandshakeId)
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Pacote " + header.PacketId + " com clientId -1 fora do Hello (drop)");
                return Task.CompletedTask;
            }
            Session.Session sender;
            if (!_sessions.TryGet(header.ClientId, out sender))
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Pacote " + header.PacketId + " de sessao desconhecida " + header.ClientId + " (drop)");
                return Task.CompletedTask;
            }
            if (sender.Token != header.Token)
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Token invalido na sessao " + header.ClientId + " (drop)");
                return Task.CompletedTask;
            }
            _sessions.Touch(sender);

            if (header.PacketId == (int)PacketType.PLAYER_STATE)
            {
                if (!sender.IsReady)
                {
                    _log.Log(ServerLogLevel.Debug, "SESSAO", "Snapshot de " + sender.Id + " antes do Confirm (drop)");
                    return Task.CompletedTask;
                }
                if (!_relay.ShouldRelay(sender, header.Seq))
                {
                    return Task.CompletedTask;
                }
                return RelayAsync(datagram.Data, sender, ct);
            }

            // Criticos (D-10): relay imediato dos bytes originais, com retry
            // por destino ate o ACK (ou 3 tentativas). A Task 3 deste plano
            // especializa chat (-5) e DISCONNECT (4) com regras de conteudo.
            if (IsCriticalPacket(header.PacketId))
            {
                if (!sender.IsReady)
                {
                    _log.Log(ServerLogLevel.Debug, "SESSAO", "Critico " + header.PacketId + " de " + sender.Id + " antes do Confirm (drop)");
                    return Task.CompletedTask;
                }
                return RelayReliableAsync(datagram.Data, sender, header.Seq, ct);
            }

            // Demais pacotes de jogo (nao-criticos fora PLAYER_STATE) ganham
            // handlers no plano 02-03.
            _log.Log(ServerLogLevel.Debug, "NET2", "Pacote " + header.PacketId + " de " + sender.Id + " sem handler neste plano (drop)");
            return Task.CompletedTask;
        }

        private async Task RelayAsync(byte[] originalDatagram, Session.Session sender, CancellationToken ct)
        {
            foreach (Session.Session target in _relay.Targets(sender, _sessions.All))
            {
                try
                {
                    await _transport.SendAsync(originalDatagram, target.EndPoint, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Log(ServerLogLevel.Warning, "NET2", "Relay para " + target.Id + " falhou: " + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// Criticos (D-10, lista em <see cref="IsCriticalPacket"/>): chat -5,
        /// CONFIG_SYNC 16, TELEPORT_REQUEST 15, SYNC_ABILITY 10, SYNC_LEVER 11,
        /// SYNC_DOOR 12, SYNC_WORLDEVENT 14, SKILL 7, COLOR 6, DISCONNECT 4.
        /// PLAYER_STATE 18 e Ping 104 nunca geram pendencia (unreliable).
        /// </summary>
        private static bool IsCriticalPacket(int packetId)
        {
            return packetId == ChatPacketId
                || packetId == (int)PacketType.CONFIG_SYNC
                || packetId == (int)PacketType.TELEPORT_REQUEST
                || packetId == (int)PacketType.SYNC_ABILITY
                || packetId == (int)PacketType.SYNC_LEVER
                || packetId == (int)PacketType.SYNC_DOOR
                || packetId == (int)PacketType.SYNC_WORLDEVENT
                || packetId == (int)PacketType.SKILL
                || packetId == (int)PacketType.COLOR
                || packetId == (int)PacketType.DISCONNECT;
        }

        /// <summary>
        /// Relay confiavel: reemite os bytes originais a cada destino IsReady
        /// (sem agregacao em tick, D-11) e registra pendencia por destino para
        /// retry de 250 ms ate 3x. O ACK do destino cancela os reenvios.
        /// </summary>
        private async Task RelayReliableAsync(byte[] originalDatagram, Session.Session sender, uint seq, CancellationToken ct)
        {
            foreach (Session.Session target in _relay.Targets(sender, _sessions.All))
            {
                try
                {
                    await _transport.SendAsync(originalDatagram, target.EndPoint, ct).ConfigureAwait(false);
                    _acks.Track(target.EndPoint, seq, originalDatagram, target.Id);
                }
                catch (Exception ex)
                {
                    _log.Log(ServerLogLevel.Warning, "NET2", "Relay confiavel para " + target.Id + " falhou: " + ex.GetType().Name);
                }
            }
        }

        /// <summary>
        /// Envio confiavel originado no servidor (clientId 0): codifica com
        /// flag Reliable, envia e agenda retry ate o ACK do destino.
        /// </summary>
        private async Task SendReliableAsync(IPEndPoint remote, int packetId, byte[] payload, int sessionId, CancellationToken ct)
        {
            uint seq = NextServerSeq();
            byte[] datagram;
            string error;
            if (!EnvelopeCodec.TryEncode(NetProtocol.FlagReliable, seq, NetProtocol.ServerId, 0, packetId, 0, payload, out datagram, out error))
            {
                _log.Log(ServerLogLevel.Error, "NET2", "Encode falhou: " + error);
                return;
            }
            try
            {
                await _transport.SendAsync(datagram, remote, ct).ConfigureAwait(false);
                _acks.Track(remote, seq, datagram, sessionId);
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Envio confiavel para " + remote + " falhou: " + ex.GetType().Name);
            }
        }

        /// <summary>
        /// SysAck 103 imediato para um datagrama Reliable (antes do dispatch).
        /// Payload = uint32 LE com a seq confirmada; nunca gera pendencia.
        /// </summary>
        private Task SendSysAckAsync(IPEndPoint remote, uint ackedSeq, CancellationToken ct)
        {
            byte[] payload = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), ackedSeq);
            return SendSystemAsync(remote, NetProtocol.ServerId, NetProtocol.MsgAck, payload, ct);
        }

        private void OnRetryTick(object? state)
        {
            List<AckTracker.Pending> expired;
            List<AckTracker.Pending> due;
            try
            {
                due = _acks.CollectDue(DateTime.UtcNow, out expired);
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "ACK", "Coleta de retries falhou: " + ex.GetType().Name);
                return;
            }
            for (int i = 0; i < expired.Count; i++)
            {
                AckTracker.Pending lost = expired[i];
                _log.Log(ServerLogLevel.Warning, "ACK", "Sem ACK da sessao " + lost.SessionId
                    + " para seq " + lost.Seq + " apos " + NetProtocol.MaxRetries
                    + " retries; desistindo (sessao mantida).");
            }
            for (int i = 0; i < due.Count; i++)
            {
                AckTracker.Pending retry = due[i];
                try
                {
                    // Fire-and-forget com observacao: o socket e thread-safe e
                    // o intervalo do timer (50 ms) nao deve bloquear.
                    Task sendTask = _transport.SendAsync(retry.Datagram, retry.Remote, CancellationToken.None);
                    sendTask.ContinueWith(delegate (Task t)
                    {
                        if (t.IsFaulted)
                        {
                            _log.Log(ServerLogLevel.Warning, "ACK", "Retry para sessao "
                                + retry.SessionId + " seq " + retry.Seq + " falhou: "
                                + t.Exception.GetType().Name);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (Exception ex)
                {
                    _log.Log(ServerLogLevel.Warning, "ACK", "Retry para sessao " + retry.SessionId + " falhou: " + ex.GetType().Name);
                }
            }
        }

        private void TouchSession(NetEnvelope header)
        {
            Session.Session session;
            if (header.ClientId != NetProtocol.PreHandshakeId && _sessions.TryGet(header.ClientId, out session))
            {
                if (session.Token == header.Token)
                {
                    _sessions.Touch(session);
                }
            }
        }

        private async Task SendSystemAsync(IPEndPoint remote, int clientId, int packetId, byte[] payload, CancellationToken ct)
        {
            uint seq = NextServerSeq();
            byte[] datagram;
            string error;
            if (!EnvelopeCodec.TryEncode(0, seq, clientId, 0, packetId, 0, payload, out datagram, out error))
            {
                _log.Log(ServerLogLevel.Error, "NET2", "Encode falhou: " + error);
                return;
            }
            try
            {
                await _transport.SendAsync(datagram, remote, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Envio para " + remote + " falhou: " + ex.GetType().Name);
            }
        }

        private Task SendRejectAsync(IPEndPoint remote, string reason, CancellationToken ct)
        {
            byte[] payload = EncodeLegacyString(string.IsNullOrEmpty(reason) ? "recusado" : reason);
            return SendSystemAsync(remote, NetProtocol.ServerId, NetProtocol.MsgReject, payload, ct);
        }

        private uint NextServerSeq()
        {
            unchecked
            {
                _serverSeq++;
                if (_serverSeq == 0)
                {
                    _serverSeq = 1;
                }
                return _serverSeq;
            }
        }

        private static byte[] EncodeLegacyString(string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            byte[] payload = new byte[4 + bytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), bytes.Length);
            Buffer.BlockCopy(bytes, 0, payload, 4, bytes.Length);
            return payload;
        }

        private void LogBindAddresses()
        {
            try
            {
                foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        _log.Log(ServerLogLevel.Info, "NET2", "LAN address: " + address + ":" + _port);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Nao foi possivel listar enderecos LAN: " + ex.GetType().Name);
            }
        }
    }
}
