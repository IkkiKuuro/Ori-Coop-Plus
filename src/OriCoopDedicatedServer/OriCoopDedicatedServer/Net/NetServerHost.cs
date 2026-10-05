using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OriCoop;
using OriCoopDedicatedServer.Core.CommandSystem;
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
    public sealed class NetServerHost : IGameTransport
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
        private Timer _sweepTimer = null!;
        private uint _serverSeq;

        /// <summary>
        /// Camada Game (02-03): quando ligada pelo ServerBoot, pacotes de
        /// jogo vao para GameHandlers; null preserva o comportamento 02-02.
        /// </summary>
        public GameHandlers? Game { get; set; }

        public NetServerHost(int port, int maxPlayers, ILogger log)
            : this(port, maxPlayers, log, null)
        {
        }

        public NetServerHost(int port, int maxPlayers, ILogger log, SessionManager? sessions)
        {
            _port = port;
            _maxPlayers = maxPlayers;
            _log = log ?? throw new ArgumentNullException("log");
            _transport = new UdpTransport(_log);
            _sessions = sessions ?? new SessionManager(maxPlayers, _log);
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
            // Sweeper de timeout (D-07): varre sessoes silenciosas a cada 1 s.
            _sweepTimer = new Timer(OnSweepTick, null, 1000, 1000);
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
                    if (_sweepTimer != null)
                    {
                        _sweepTimer.Dispose();
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
                    TouchSession(datagram.Remote, header);
                    break;
                default:
                    await HandleGamePacketAsync(datagram, header, payload, ct).ConfigureAwait(false);
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
            if (!_sessions.HandleConfirm(header.ClientId, header.Token, remote, out session, out rejectReason))
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Confirm recusado de " + remote + ": " + rejectReason);
                await SendRejectAsync(remote, rejectReason, ct).ConfigureAwait(false);
                return;
            }
            // Baseline do drop-old: seqs do remetente passam a valer a partir daqui.
            session.LastRecvSeq = header.Seq;
            // Camada Game (02-03): COLOR inicial + CONFIG_SYNC unicast +
            // historico de abilities no join.
            if (Game != null)
            {
                await Game.OnPlayerJoinAsync(session, ct).ConfigureAwait(false);
            }
        }

        private async Task HandlePingAsync(IPEndPoint remote, NetEnvelope header, byte[] payload, CancellationToken ct)
        {
            Session.Session session;
            string reason;
            if (!_sessions.ValidatePacket(header.ClientId, header.Token, remote, out session, out reason))
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Ping descartado de " + remote + ": " + reason);
                return;
            }
            _sessions.Touch(session);
            // Pong = eco stateless do payload (sendTicks do cliente, D-12).
            // Unreliable: nunca gera pendencia.
            await SendSystemAsync(remote, NetProtocol.ServerId, NetProtocol.MsgPong, payload, ct).ConfigureAwait(false);
        }

        private Task HandleGamePacketAsync(ReceivedDatagram datagram, NetEnvelope header, byte[] payload, CancellationToken ct)
        {
            if (header.ClientId == NetProtocol.PreHandshakeId)
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Pacote " + header.PacketId + " com clientId -1 fora do Hello (drop)");
                return Task.CompletedTask;
            }
            Session.Session sender;
            string validationReason;
            if (!_sessions.ValidatePacket(header.ClientId, header.Token, datagram.Remote, out sender, out validationReason))
            {
                _log.Log(ServerLogLevel.Warning, "NET2", "Pacote " + header.PacketId + " descartado: " + validationReason);
                return Task.CompletedTask;
            }
            _sessions.Touch(sender);

            if (header.PacketId == (int)PacketType.DISCONNECT)
            {
                if (Game != null)
                {
                    Game.OnSessionLeft(sender.Id);
                }
                return HandleDisconnectAsync(sender, ct);
            }

            if (header.PacketId == ChatPacketId)
            {
                return HandleChatAsync(sender, payload, ct);
            }

            if (header.PacketId == (int)PacketType.PLAYER_STATE)
            {
                if (!sender.IsReady)
                {
                    _log.Log(ServerLogLevel.Debug, "SESSAO", "Snapshot de " + sender.Id + " antes do Confirm (drop)");
                    return Task.CompletedTask;
                }
                // Camada Game (02-03): registra LastKnown + relay unreliable.
                if (Game != null)
                {
                    return Game.HandlePlayerStateAsync(sender, header.Seq, payload, datagram.Data, ct);
                }
                if (!_relay.ShouldRelay(sender, header.Seq))
                {
                    return Task.CompletedTask;
                }
                return RelayAsync(datagram.Data, sender, ct);
            }

            // Camada Game (02-03, via ServerBoot): pacotes de jogo vao para
            // GameHandlers (gating, autoridade, carimbo do remetente).
            if (Game != null && GameHandlers.IsGamePacket(header.PacketId))
            {
                if (!sender.IsReady)
                {
                    _log.Log(ServerLogLevel.Debug, "SESSAO", "Jogo " + header.PacketId + " de " + sender.Id + " antes do Confirm (drop)");
                    return Task.CompletedTask;
                }
                return Game.DispatchAsync(sender, header.Seq, header.PacketId, payload, datagram.Data, ct);
            }

            // Criticos (D-10): relay imediato dos bytes originais, com retry
            // por destino ate o ACK (ou 3 tentativas). Vale quando a camada
            // Game nao esta ligada (comportamento 02-02); com Game, os
            // pacotes de jogo sobem para os handlers acima.
            if (IsCriticalPacket(header.PacketId))
            {
                if (!sender.IsReady)
                {
                    _log.Log(ServerLogLevel.Debug, "SESSAO", "Critico " + header.PacketId + " de " + sender.Id + " antes do Confirm (drop)");
                    return Task.CompletedTask;
                }
                return RelayReliableAsync(datagram.Data, sender, header.Seq, ct);
            }

            // Demais pacotes nao-criticos fora PLAYER_STATE e fora da camada
            // Game: sem handler neste plano (drop).
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
        /// Chat -5 (D-10/D-11, regras preservadas do chat atual): trunca em
        /// 350 chars, remove &lt;/&gt; do texto e do nick, responde help
        /// (h/help//h//help) em unicast confiavel ao solicitante e transmite
        /// o restante com sender formatado, confiavel, aos IsReady.
        /// Corpo de entrada: marcador int -5 + string legada (tolerante a
        /// string nua sem marcador). Corpo de saida: marcador -5 + sender +
        /// texto, como o cliente new-core le.
        /// </summary>
        private async Task HandleChatAsync(Session.Session sender, byte[] payload, CancellationToken ct)
        {
            if (!sender.IsReady)
            {
                _log.Log(ServerLogLevel.Debug, "SESSAO", "Chat de " + sender.Id + " antes do Confirm (drop)");
                return;
            }
            string text;
            if (!TryParseChatText(payload, out text))
            {
                _log.Log(ServerLogLevel.Warning, "CHAT", "Texto ilegivel de " + sender.Id + " (drop)");
                return;
            }
            if (text.Length > NetProtocol.ChatMaxChars)
            {
                text = text.Substring(0, NetProtocol.ChatMaxChars);
            }
            text = StripBrackets(text);
            string safeNick = StripBrackets(sender.Nickname);

            if (IsHelpCommand(text))
            {
                byte[] helpPayload = BuildChatPayload("<color=yellow>SERVER</color>", BuildHelpText());
                await SendReliableAsync(sender.EndPoint, ChatPacketId, helpPayload, sender.Id, ct).ConfigureAwait(false);
                return;
            }

            byte[] broadcast = BuildChatPayload("<color=green>" + safeNick + "</color>", text);
            _log.Log(ServerLogLevel.Info, "CHAT", "[" + safeNick + " " + sender.Id + "] " + text);
            foreach (Session.Session target in _relay.Targets(sender, _sessions.All))
            {
                try
                {
                    uint seq = NextServerSeq();
                    byte[] datagram;
                    string error;
                    if (!EnvelopeCodec.TryEncode(NetProtocol.FlagReliable, seq, NetProtocol.ServerId, 0, ChatPacketId, 0, broadcast, out datagram, out error))
                    {
                        _log.Log(ServerLogLevel.Error, "NET2", "Encode falhou: " + error);
                        return;
                    }
                    await _transport.SendAsync(datagram, target.EndPoint, ct).ConfigureAwait(false);
                    _acks.Track(target.EndPoint, seq, datagram, target.Id);
                }
                catch (Exception ex)
                {
                    _log.Log(ServerLogLevel.Warning, "NET2", "Chat para " + target.Id + " falhou: " + ex.GetType().Name);
                }
            }
        }

        private static bool TryParseChatText(byte[] payload, out string text)
        {
            text = string.Empty;
            if (payload == null)
            {
                return false;
            }
            int offset = 0;
            if (payload.Length >= 4 && BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) == ChatPacketId)
            {
                offset = 4;
            }
            string value;
            int next;
            if (!TryReadLegacyString(payload, offset, out value, out next))
            {
                return false;
            }
            text = value ?? string.Empty;
            return true;
        }

        private static bool TryReadLegacyString(byte[] buffer, int offset, out string value, out int nextOffset)
        {
            value = string.Empty;
            nextOffset = offset;
            if (buffer == null || offset < 0 || buffer.Length - offset < 4)
            {
                return false;
            }
            int length = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4));
            if (length < 0 || length > buffer.Length - offset - 4)
            {
                return false;
            }
            try
            {
                value = Encoding.ASCII.GetString(buffer, offset + 4, length);
            }
            catch (Exception)
            {
                return false;
            }
            nextOffset = offset + 4 + length;
            return true;
        }

        private static byte[] BuildChatPayload(string sender, string text)
        {
            byte[] senderBytes = Encoding.ASCII.GetBytes(sender ?? string.Empty);
            byte[] textBytes = Encoding.ASCII.GetBytes(text ?? string.Empty);
            byte[] payload = new byte[4 + 4 + senderBytes.Length + 4 + textBytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), ChatPacketId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), senderBytes.Length);
            Buffer.BlockCopy(senderBytes, 0, payload, 8, senderBytes.Length);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8 + senderBytes.Length, 4), textBytes.Length);
            Buffer.BlockCopy(textBytes, 0, payload, 12 + senderBytes.Length, textBytes.Length);
            return payload;
        }

        private static string StripBrackets(string value)
        {
            return (value ?? string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);
        }

        private static bool IsHelpCommand(string text)
        {
            string normalized = (text ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "h" || normalized == "help" || normalized == "/h" || normalized == "/help";
        }

        private string BuildHelpText()
        {
            var names = new List<string>();
            try
            {
                foreach (ConsoleCommand cmd in CommandProcessor.AllCommands)
                {
                    if (cmd != null && !string.IsNullOrEmpty(cmd.Command))
                    {
                        names.Add("/" + cmd.Command);
                    }
                }
            }
            catch (Exception)
            {
            }
            if (names.Count == 0)
            {
                names.Add("/coop");
                names.Add("/tp");
                names.Add("/dummy");
                names.Add("/clientcolors");
                names.Add("/entitysync");
            }
            var sb = new StringBuilder("Commands: ");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// DISCONNECT do proprio cliente (D-07/D-10): remove a sessao e
        /// avisa os restantes com DISCONNECT confiavel. Payload de aviso =
        /// marcador int 4 + disconnectedId int (lido pelo cliente new-core).
        /// </summary>
        private async Task HandleDisconnectAsync(Session.Session sender, CancellationToken ct)
        {
            Session.Session removed;
            if (!_sessions.Remove(sender.Id, out removed))
            {
                return;
            }
            _acks.PurgeFor(sender.EndPoint);
            _log.Log(ServerLogLevel.Info, "SESSAO", "ID " + sender.Id + " (" + sender.Nickname + ") desconectou");
            await BroadcastDisconnectAsync(sender.Id, ct).ConfigureAwait(false);
        }

        private async Task BroadcastDisconnectAsync(int disconnectedId, CancellationToken ct)
        {
            byte[] payload = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.DISCONNECT);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), disconnectedId);
            foreach (Session.Session target in _sessions.All)
            {
                if (!target.IsReady)
                {
                    continue;
                }
                await SendReliableAsync(target.EndPoint, (int)PacketType.DISCONNECT, payload, target.Id, ct).ConfigureAwait(false);
            }
        }

        private void OnSweepTick(object? state)
        {
            List<Session.Session> expired;
            try
            {
                expired = _sessions.SweepExpired(NetProtocol.SessionTimeoutMs);
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "SESSAO", "Sweeper falhou: " + ex.GetType().Name);
                return;
            }
            for (int i = 0; i < expired.Count; i++)
            {
                Session.Session gone = expired[i];
                _acks.PurgeFor(gone.EndPoint);
                if (Game != null)
                {
                    Game.OnSessionLeft(gone.Id);
                }
                _log.Log(ServerLogLevel.Info, "SESSAO", "ID " + gone.Id + " (" + gone.Nickname
                    + ") removido apos " + NetProtocol.SessionTimeoutMs + " ms sem datagrama");
                try
                {
                    // Fire-and-forget observado: aviso confiavel aos restantes.
                    BroadcastDisconnectAsync(gone.Id, CancellationToken.None).ContinueWith(delegate (Task t)
                    {
                        if (t.IsFaulted)
                        {
                            _log.Log(ServerLogLevel.Warning, "SESSAO", "Aviso de saida de " + gone.Id
                                + " falhou: " + t.Exception.GetType().Name);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (Exception ex)
                {
                    _log.Log(ServerLogLevel.Warning, "SESSAO", "Aviso de saida de " + gone.Id + " falhou: " + ex.GetType().Name);
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
        /// Superficie de envio da camada Game (02-03, via IGameTransport):
        /// unicast/broadcast confiavel, relay cru e chat originados no
        /// servidor, mais snapshot do dummy (clientId 999, unreliable).
        /// </summary>
        async Task IGameTransport.UnicastReliableAsync(Session.Session session, int packetId, byte[] payload, CancellationToken ct)
        {
            if (session == null)
            {
                return;
            }
            await SendReliableAsync(session.EndPoint, packetId, payload, session.Id, ct).ConfigureAwait(false);
        }

        async Task IGameTransport.BroadcastReliableAsync(int packetId, byte[] payload, CancellationToken ct)
        {
            await GameBroadcastReliableAsync(packetId, payload, ct).ConfigureAwait(false);
        }

        Task IGameTransport.RelayUnreliableAsync(byte[] originalDatagram, Session.Session sender, CancellationToken ct)
        {
            return RelayAsync(originalDatagram, sender, ct);
        }

        Task IGameTransport.RelayReliableAsync(byte[] originalDatagram, Session.Session sender, uint seq, CancellationToken ct)
        {
            return RelayReliableAsync(originalDatagram, sender, seq, ct);
        }

        Task IGameTransport.ChatUnicastAsync(Session.Session session, string sender, string text, CancellationToken ct)
        {
            if (session == null)
            {
                return Task.CompletedTask;
            }
            return SendReliableAsync(session.EndPoint, ChatPacketId, BuildChatPayload(sender, text), session.Id, ct);
        }

        async Task IGameTransport.ChatBroadcastAsync(string sender, string text, CancellationToken ct)
        {
            byte[] broadcast = BuildChatPayload(sender, text);
            foreach (Session.Session target in _sessions.All)
            {
                if (!target.IsReady)
                {
                    continue;
                }
                try
                {
                    uint seq = NextServerSeq();
                    byte[] datagram;
                    string error;
                    if (!EnvelopeCodec.TryEncode(NetProtocol.FlagReliable, seq, NetProtocol.ServerId, 0, ChatPacketId, 0, broadcast, out datagram, out error))
                    {
                        _log.Log(ServerLogLevel.Error, "NET2", "Encode falhou: " + error);
                        return;
                    }
                    await _transport.SendAsync(datagram, target.EndPoint, ct).ConfigureAwait(false);
                    _acks.Track(target.EndPoint, seq, datagram, target.Id);
                }
                catch (Exception ex)
                {
                    _log.Log(ServerLogLevel.Warning, "NET2", "Chat do servidor para " + target.Id + " falhou: " + ex.GetType().Name);
                }
            }
        }

        Task IGameTransport.BroadcastStateAsync(byte[] statePayload, CancellationToken ct)
        {
            return GameBroadcastStateAsync(statePayload, ct);
        }

        /// <summary>
        /// Broadcast confiavel a todas as sessoes prontas (CONFIG_SYNC pos-
        /// mudanca, relays reconstruidos com carimbo do remetente).
        /// </summary>
        public async Task GameBroadcastReliableAsync(int packetId, byte[] payload, CancellationToken ct)
        {
            foreach (Session.Session target in _sessions.All)
            {
                if (!target.IsReady)
                {
                    continue;
                }
                await SendReliableAsync(target.EndPoint, packetId, payload, target.Id, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Snapshot do dummy: envelope unreliable com clientId 999 (fora do
        /// allocator) a todas as sessoes prontas. Corpo no formato que o
        /// cliente new-core le (sem carimbo — ver BuildPlayerStatePayload).
        /// </summary>
        public async Task GameBroadcastStateAsync(byte[] statePayload, CancellationToken ct)
        {
            foreach (Session.Session target in _sessions.All)
            {
                if (!target.IsReady)
                {
                    continue;
                }
                uint seq = NextServerSeq();
                byte[] datagram;
                string error;
                if (!EnvelopeCodec.TryEncode(0, seq, NetProtocol.DummyId, 0, (int)PacketType.PLAYER_STATE, 0, statePayload, out datagram, out error))
                {
                    _log.Log(ServerLogLevel.Error, "NET2", "Encode do dummy falhou: " + error);
                    return;
                }
                try
                {
                    await _transport.SendAsync(datagram, target.EndPoint, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Log(ServerLogLevel.Warning, "NET2", "Snapshot do dummy para " + target.Id + " falhou: " + ex.GetType().Name);
                }
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

        private void TouchSession(IPEndPoint remote, NetEnvelope header)
        {
            Session.Session session;
            string reason;
            if (header.ClientId != NetProtocol.PreHandshakeId
                && _sessions.ValidatePacket(header.ClientId, header.Token, remote, out session, out reason))
            {
                _sessions.Touch(session);
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
