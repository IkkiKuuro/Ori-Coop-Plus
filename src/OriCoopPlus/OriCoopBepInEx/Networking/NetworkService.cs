using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using OriCoop;
using OriCoopBepInEx.Domain;
using UnityEngine;

namespace OriCoopBepInEx.Networking
{
    // Contraparte cliente do envelope versionado (fase 2, D-14/D-15):
    // fala EXCLUSIVAMENTE o novo envelope do mesmo build, sem fallback legado.
    // Header 24B little-endian via BinaryWriter campo-a-campo nos offsets de
    // NetProtocol; corpos de jogo preservados byte-identicos ao legado
    // (PLAYER_STATE inclui leading int 18; o playerId do remetente viaja no
    // header clientId — o servidor reemite os bytes sem reconstrucao).
    //
    // Confiabilidade (D-10): todo datagrama com flag Reliable recebido gera
    // SysAck 103 imediato; todo envio confiavel (chat, TELEPORT 15, SKILL 7,
    // COLOR 6, DISCONNECT 4, SYNC_*) e reenviado a cada 250 ms ate 3 tentativas
    // ate o SysAck do servidor. PLAYER_STATE e Ping seguem unreliable, sem
    // pendencia (D-09). Recepcao de snapshots com drop-old wrap-safe por
    // remetente (D-11). Heartbeat de estado + Ping rodam na thread de rede com
    // ReceiveTimeout; o jogo so enfileira snapshots, nunca envia direto —
    // nenhum envio ocorre em FixedUpdate (D-07).
    public sealed class NetworkService : INetworkService
    {
        // Chat usa o ID -5 do contrato atual (nao e legado: o servidor o
        // trata como pacote critico confiavel). Os pacotes legados de IDs
        // negativos (handshake antigo, NBMessage, NetworkVar, RPC e ping
        // antigo) nao existem mais neste arquivo: a variavel de rede
        // NetworkVar foi absorvida pelos 2 bools finais do CONFIG_SYNC.
        private const int ChatPacketId = -5;
        private const long PingIntervalTicks = TimeSpan.TicksPerSecond * 2;
        private const long HelloIntervalTicks = TimeSpan.TicksPerMillisecond * 500;
        private const long HeartbeatIntervalTicks = TimeSpan.TicksPerMillisecond * 400;

        private sealed class PendingSend
        {
            public uint Seq;
            public byte[] Datagram;
            public long NextRetryTicks;
            public int Attempts;
        }

        private readonly UdpClient _client;
        private readonly IPEndPoint _server;
        private readonly object _sync = new object();
        private readonly Dictionary<int, uint> _lastRelaySeq = new Dictionary<int, uint>();
        private readonly Dictionary<uint, PendingSend> _pending = new Dictionary<uint, PendingSend>();
        private Thread _receiveThread;
        private bool _running;
        private int _assignedId = -1;
        private uint _sessionToken;
        private uint _sendSeq;
        private PlayerSnapshot _queuedSnapshot;
        private string _nickname;
        private string _lastRejectReason = string.Empty;
        private long _lastPingSentTicks;
        private long _lastHelloSentTicks;
        private byte _lastSentState = 255;
        private int _lastSentHash;
        private Vector3Data _lastSentPos;
        private long _lastSentTicks;

        public bool IsConnected
        {
            get { return _assignedId >= 0; }
        }

        public string LastRejectReason
        {
            get { return _lastRejectReason; }
        }

        public event Action<PlayerSnapshot> PlayerSnapshotReceived;
        public event Action<Vector3Data, string> TeleportRequested;
        public event Action<string, string> ChatMessageReceived;
        public event Action<bool> EntitySyncChanged;
        public event Action<int> PingUpdated;
        public event Action<string, int> IdentityAssigned;
        public event Action<int> PlayerDisconnected;
        public event ConfigSyncHandler ConfigSyncReceived;

        public NetworkService(string host, int port, int playerId, string nickname)
        {
            if (string.IsNullOrEmpty(host))
            {
                throw new ArgumentException("Network host is required.", "host");
            }
            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException("port");
            }

            _client = new UdpClient(AddressFamily.InterNetwork);
            _client.Client.ReceiveTimeout = 250;
            try
            {
                // Ignora ICMP Port Unreachable (ex.: servidor reiniciado) em
                // vez de estourar ConnectionReset no Receive.
                _client.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
            }
            catch { }
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            IPAddress serverAddress = null;
            for (int i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    serverAddress = addresses[i];
                    break;
                }
            }
            if (serverAddress == null)
            {
                _client.Close();
                throw new ArgumentException("Network host must resolve to an IPv4 address.", "host");
            }
            _server = new IPEndPoint(serverAddress, port);
            // IDs sao atribuidos pelo servidor no handshake (D-05/D-06);
            // o PlayerId da config e apenas dica e nao e mais usado direto.
            _assignedId = -1;
            _sessionToken = 0;
            _nickname = StripBrackets(string.IsNullOrEmpty(nickname) ? "NONICK" : nickname.Trim());
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_running)
                {
                    return;
                }

                _running = true;
                _receiveThread = new Thread(ReceiveLoop);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();
            }
        }

        public void SendPlayerSnapshot(PlayerSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }
            // Enfileira apenas: o envio real acontece na thread de rede
            // (FlushSnapshot), nunca na thread do jogo/FixedUpdate (D-07).
            lock (_sync)
            {
                _queuedSnapshot = snapshot;
            }
        }

        private void FlushSnapshot()
        {
            PlayerSnapshot snapshot;
            lock (_sync)
            {
                snapshot = _queuedSnapshot;
                _queuedSnapshot = null;
            }
            if (snapshot == null)
            {
                return;
            }
            if (_assignedId < 0)
            {
                return;
            }

            byte stateByte = (byte)snapshot.Animation.State;
            int stateHash = unchecked((int)snapshot.Animation.AnimNameHash);
            long nowTicks = DateTime.UtcNow.Ticks;
            bool stateChanged = stateByte != _lastSentState || stateHash != _lastSentHash;
            bool posChanged = !SamePosition(snapshot.Position, _lastSentPos);
            bool heartbeatDue = nowTicks - _lastSentTicks >= HeartbeatIntervalTicks;
            if (!stateChanged && !posChanged && !heartbeatDue)
            {
                return;
            }
            _lastSentState = stateByte;
            _lastSentHash = stateHash;
            _lastSentPos = snapshot.Position;
            _lastSentTicks = nowTicks;

            using (MemoryStream stateBody = new MemoryStream())
            using (BinaryWriter stateWriter = new BinaryWriter(stateBody))
            {
                stateWriter.Write((int)PacketType.PLAYER_STATE);
                stateWriter.Write(snapshot.Position.X);
                stateWriter.Write(snapshot.Position.Y);
                stateWriter.Write(snapshot.Position.Z);
                stateWriter.Write(stateByte);
                byte flags = 0;
                if (snapshot.Animation.FacingLeft)
                {
                    flags |= 1;
                }
                if (snapshot.Animation.IsGrounded)
                {
                    flags |= 2;
                }
                stateWriter.Write(flags);
                stateWriter.Write(stateHash);
                stateWriter.Write(snapshot.Velocity.X);
                stateWriter.Write(snapshot.Velocity.Y);
                WriteLegacyString(stateWriter, !string.IsNullOrEmpty(snapshot.Nick) ? snapshot.Nick : _nickname);
                stateWriter.Flush();
                // Snapshots sao unreliable por desenho (D-09): sem retry.
                SendSystem((int)PacketType.PLAYER_STATE, stateBody.ToArray(), 0);
            }

        }

        private static bool SamePosition(Vector3Data a, Vector3Data b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz < 0.0025f;
        }

        public void SendTeleportRequest(int targetPlayerId)
        {
            if (_assignedId < 0 || targetPlayerId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.TELEPORT_REQUEST);
                writer.Write(targetPlayerId);
                writer.Flush();
                SendReliable((int)PacketType.TELEPORT_REQUEST, body.ToArray());
            }
        }

        public void SendChatMessage(string text)
        {
            if (_assignedId < 0 || string.IsNullOrEmpty(text))
            {
                return;
            }

            string clipped = text.Length > NetProtocol.ChatMaxChars
                ? text.Substring(0, NetProtocol.ChatMaxChars)
                : text;
            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write(ChatPacketId);
                WriteLegacyString(writer, clipped);
                writer.Flush();
                SendReliable(ChatPacketId, body.ToArray());
            }
        }

        public void SendSkill(int skillId)
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.SKILL);
                writer.Write(skillId);
                writer.Flush();
                SendReliable((int)PacketType.SKILL, body.ToArray());
            }
        }

        public void SendColor(byte r, byte g, byte b)
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.COLOR);
                writer.Write(r);
                writer.Write(g);
                writer.Write(b);
                writer.Flush();
                SendReliable((int)PacketType.COLOR, body.ToArray());
            }
        }

        public void SendDisconnect()
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.DISCONNECT);
                writer.Write(_assignedId);
                writer.Flush();
                SendReliable((int)PacketType.DISCONNECT, body.ToArray());
            }
        }

        public void SendSyncAbility(int abilityId)
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.SYNC_ABILITY);
                writer.Write(abilityId);
                writer.Flush();
                SendReliable((int)PacketType.SYNC_ABILITY, body.ToArray());
            }
        }

        public void SendSyncLever(int v0, int v1, int v2, int v3, int v4)
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.SYNC_LEVER);
                writer.Write(v0);
                writer.Write(v1);
                writer.Write(v2);
                writer.Write(v3);
                writer.Write(v4);
                writer.Flush();
                SendReliable((int)PacketType.SYNC_LEVER, body.ToArray());
            }
        }

        public void SendSyncDoor(int v0, int v1, int v2, int v3)
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.SYNC_DOOR);
                writer.Write(v0);
                writer.Write(v1);
                writer.Write(v2);
                writer.Write(v3);
                writer.Flush();
                SendReliable((int)PacketType.SYNC_DOOR, body.ToArray());
            }
        }

        public void SendSyncWorldEvent(int v0, int v1, int v2, int v3, int v4)
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.SYNC_WORLDEVENT);
                writer.Write(v0);
                writer.Write(v1);
                writer.Write(v2);
                writer.Write(v3);
                writer.Write(v4);
                writer.Flush();
                SendReliable((int)PacketType.SYNC_WORLDEVENT, body.ToArray());
            }
        }

        public void SendDummyAction(int action)
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.DUMMY_ACTION);
                writer.Write(action);
                writer.Flush();
                SendReliable((int)PacketType.DUMMY_ACTION, body.ToArray());
            }
        }

        public void SendDummyAction(int action, int abilityId)
        {
            if (_assignedId < 0)
            {
                return;
            }

            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write((int)PacketType.DUMMY_ACTION);
                writer.Write(action);
                writer.Write(abilityId);
                writer.Flush();
                SendReliable((int)PacketType.DUMMY_ACTION, body.ToArray());
            }
        }

        public void SendNicknameUpdate(string newNick)
        {
            if (string.IsNullOrEmpty(newNick))
            {
                return;
            }

            _nickname = StripBrackets(newNick.Trim());
            if (_assignedId >= 0)
            {
                using (MemoryStream body = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(body))
                {
                    WriteLegacyString(writer, _nickname);
                    writer.Flush();
                    SendSystem(NetProtocol.MsgConfirm, body.ToArray(), 0);
                }
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    long nowTicks = DateTime.UtcNow.Ticks;
                    if (_assignedId < 0)
                    {
                        // Hello com intervalo: o timeout curto (250 ms) serve
                        // a bomba de retry, nao pode virar spam de handshake.
                        if (nowTicks - _lastHelloSentTicks >= HelloIntervalTicks)
                        {
                            _lastHelloSentTicks = nowTicks;
                            SendHello();
                        }
                    }
                    else if (nowTicks - _lastPingSentTicks >= PingIntervalTicks)
                    {
                        SendPing();
                    }

                    PumpRetries();

                    // Dreno do snapshot mais recente enfileirado pelo jogo:
                    // on-change + heartbeat 2,5 Hz, sempre na thread de rede.
                    FlushSnapshot();

                    byte[] datagram = _client.Receive(ref endpoint);
                    ReadServerPacket(datagram);
                }

                catch (SocketException)
                {
                    // Timeout allows connection retries and shutdown checks.
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (InvalidDataException)
                {
                    // Ignore malformed packets without stopping synchronization.
                }
                catch (EndOfStreamException)
                {
                    // Ignore truncated packets without stopping synchronization.
                }
            }
        }

        private void SendPing()
        {
            long sentTicks = DateTime.UtcNow.Ticks;
            _lastPingSentTicks = sentTicks;
            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write(sentTicks);
                writer.Flush();
                // Ping e unreliable (D-09): nunca gera pendencia.
                SendSystem(NetProtocol.MsgPing, body.ToArray(), 0);
            }
        }

        private void SendHello()
        {
            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write(NetProtocol.Version);
                WriteLegacyString(writer, _nickname);
                writer.Flush();
                SendSystemPreHandshake(NetProtocol.MsgHello, body.ToArray());
            }
        }

        private void SendConfirm()
        {
            SendSystem(NetProtocol.MsgConfirm, new byte[0], 0);
        }

        private void SendSystem(int packetId, byte[] body, byte flags)
        {
            uint seq = NextSeq();
            SendEnvelope(_assignedId, _sessionToken, packetId, seq, flags, body);
        }

        private void SendReliable(int packetId, byte[] body)
        {
            uint seq = NextSeq();
            byte[] datagram = BuildEnvelope(_assignedId, _sessionToken, packetId, seq, NetProtocol.FlagReliable, body);
            lock (_sync)
            {
                PendingSend pending = new PendingSend();
                pending.Seq = seq;
                pending.Datagram = datagram;
                pending.Attempts = 0;
                pending.NextRetryTicks = DateTime.UtcNow.Ticks + NetProtocol.AckRetryMs * TimeSpan.TicksPerMillisecond;
                _pending[seq] = pending;
            }
            SendRaw(datagram);
        }

        private void SendSysAck(uint ackedSeq)
        {
            using (MemoryStream body = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(body))
            {
                writer.Write(ackedSeq);
                writer.Flush();
                // SysAck nunca gera pendencia.
                SendEnvelope(_assignedId, _sessionToken, NetProtocol.MsgAck, NextSeq(), 0, body.ToArray());
            }
        }

        private void Complete(uint ackedSeq)
        {
            lock (_sync)
            {
                _pending.Remove(ackedSeq);
            }
        }

        private void PumpRetries()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            List<PendingSend> due = null;
            List<uint> expired = null;
            lock (_sync)
            {
                foreach (KeyValuePair<uint, PendingSend> pair in _pending)
                {
                    PendingSend pending = pair.Value;
                    if (nowTicks < pending.NextRetryTicks)
                    {
                        continue;
                    }
                    if (pending.Attempts >= NetProtocol.MaxRetries)
                    {
                        if (expired == null)
                        {
                            expired = new List<uint>();
                        }
                        expired.Add(pair.Key);
                    }
                    else
                    {
                        pending.Attempts++;
                        pending.NextRetryTicks = nowTicks + NetProtocol.AckRetryMs * TimeSpan.TicksPerMillisecond;
                        if (due == null)
                        {
                            due = new List<PendingSend>();
                        }
                        due.Add(pending);
                    }
                }
                if (expired != null)
                {
                    for (int i = 0; i < expired.Count; i++)
                    {
                        _pending.Remove(expired[i]);
                    }
                }
            }
            if (due != null)
            {
                for (int i = 0; i < due.Count; i++)
                {
                    try
                    {
                        SendRaw(due[i].Datagram);
                    }
                    catch (Exception)
                    {
                        // Socket fechando no Dispose: pendencias morrem junto.
                    }
                }
            }
        }

        private void SendSystemPreHandshake(int packetId, byte[] body)
        {
            uint seq = NextSeq();
            SendEnvelope(NetProtocol.PreHandshakeId, 0, packetId, seq, 0, body);
        }

        private uint NextSeq()
        {
            unchecked
            {
                _sendSeq++;
                if (_sendSeq == 0)
                {
                    _sendSeq = 1;
                }
                return _sendSeq;
            }
        }

        private void SendEnvelope(int clientId, uint token, int packetId, uint seq, byte flags, byte[] body)
        {
            SendRaw(BuildEnvelope(clientId, token, packetId, seq, flags, body));
        }

        private static byte[] BuildEnvelope(int clientId, uint token, int packetId, uint seq, byte flags, byte[] body)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(NetProtocol.Magic);
                writer.Write(NetProtocol.Version);
                writer.Write(flags);
                writer.Write(seq);
                writer.Write(clientId);
                writer.Write(token);
                writer.Write(packetId);
                writer.Write((uint)0);
                if (body != null && body.Length > 0)
                {
                    writer.Write(body);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private void SendRaw(byte[] payload)
        {
            _client.Send(payload, payload.Length, _server);
        }

        private void ReadServerPacket(byte[] datagram)
        {
            if (datagram == null || datagram.Length < NetProtocol.HeaderSize)
            {
                return;
            }
            using (MemoryStream stream = new MemoryStream(datagram))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                ushort magic = reader.ReadUInt16();
                byte version = reader.ReadByte();
                byte flags = reader.ReadByte();
                uint seq = reader.ReadUInt32();
                int headerClientId = reader.ReadInt32();
                reader.ReadUInt32(); // token (servidor nao o ecoa)
                int packetId = reader.ReadInt32();
                uint ackSeq = reader.ReadUInt32();
                if (magic != NetProtocol.Magic || version != NetProtocol.Version)
                {
                    return;
                }
                // Todo Reliable recebido gera SysAck imediato (D-10), antes
                // do dispatch; ACK piggybacked vale como SysAck.
                if ((flags & NetProtocol.FlagReliable) != 0)
                {
                    SendSysAck(seq);
                }
                if ((flags & NetProtocol.FlagAckPresent) != 0)
                {
                    Complete(ackSeq);
                }
                if (packetId == NetProtocol.MsgAck)
                {
                    uint ackedSeq = reader.ReadUInt32();
                    Complete(ackedSeq);
                    return;
                }
                if (packetId == NetProtocol.MsgWelcome)
                {
                    int assignedId = reader.ReadInt32();
                    uint token = reader.ReadUInt32();
                    reader.ReadByte(); // serverVer
                    _assignedId = assignedId;
                    _sessionToken = token;
                    // Nova sessao: baselines e pendencias antigas morrem aqui;
                    // o re-sync pos-Confirm (CONFIG + snapshots que o servidor
                    // reenvia) e aplicado pelos handlers abaixo.
                    _lastRelaySeq.Clear();
                    lock (_sync)
                    {
                        _pending.Clear();
                    }
                    SendConfirm();
                    Action<string, int> handler = IdentityAssigned;
                    if (handler != null)
                    {
                        handler(_nickname, _assignedId);
                    }
                    return;
                }
                if (packetId == NetProtocol.MsgPong)
                {
                    long sentTicks = reader.ReadInt64();
                    int ping = (int)Math.Max(0L, (DateTime.UtcNow.Ticks - sentTicks) / TimeSpan.TicksPerMillisecond);
                    Action<int> handler = PingUpdated;
                    if (handler != null)
                    {
                        handler(ping);
                    }
                    return;
                }
                if (packetId == NetProtocol.MsgReject)
                {
                    string reason = ReadLegacyString(reader);
                    _lastRejectReason = reason;
                    // Sessao morta no servidor (restart/timeout): volta ao handshake (D-07).
                    _assignedId = -1;
                    _sessionToken = 0;
                    lock (_sync)
                    {
                        _pending.Clear();
                    }
                    return;
                }
                if (packetId == (int)PacketType.PLAYER_STATE)
                {
                    int marker = reader.ReadInt32();
                    if (marker != (int)PacketType.PLAYER_STATE)
                    {
                        throw new InvalidDataException("PLAYER_STATE sem marcador 18.");
                    }
                    if (!IsNewerThanLast(headerClientId, seq))
                    {
                        return;
                    }
                    PlayerSnapshot snapshot = new PlayerSnapshot();
                    snapshot.PlayerId = headerClientId;
                    snapshot.Position = new Vector3Data(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    snapshot.Animation.State = (ActionVisualState)reader.ReadByte();
                    byte stateFlags = reader.ReadByte();
                    snapshot.Animation.FacingLeft = (stateFlags & 1) != 0;
                    snapshot.Animation.IsGrounded = (stateFlags & 2) != 0;
                    snapshot.Animation.AnimNameHash = unchecked((uint)reader.ReadInt32());
                    snapshot.Velocity = new Vector2Data(reader.ReadSingle(), reader.ReadSingle());
                    snapshot.Nick = ReadLegacyString(reader);
                    snapshot.IsPlayerStatePacket = true;
                    snapshot.Timestamp = DateTime.UtcNow.Ticks;
                    RaiseSnapshot(snapshot);
                }
                else if (packetId == (int)PacketType.TELEPORT_REQUEST)
                {
                    int marker = reader.ReadInt32();
                    if (marker != (int)PacketType.TELEPORT_REQUEST)
                    {
                        throw new InvalidDataException("TELEPORT sem marcador.");
                    }
                    Vector3Data position = new Vector3Data(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                    string destination = ReadLegacyString(reader);
                    Action<Vector3Data, string> handler = TeleportRequested;
                    if (handler != null)
                    {
                        handler(position, destination);
                    }
                }

                else if (packetId == ChatPacketId)
                {
                    int marker = reader.ReadInt32();
                    if (marker != ChatPacketId)
                    {
                        throw new InvalidDataException("Chat sem marcador.");
                    }
                    string sender = ReadLegacyString(reader);
                    string message = ReadLegacyString(reader);
                    Action<string, string> handler = ChatMessageReceived;
                    if (handler != null)
                    {
                        handler(sender, message);
                    }
                }
                else if (packetId == (int)PacketType.DISCONNECT)
                {
                    int marker = reader.ReadInt32();
                    if (marker != (int)PacketType.DISCONNECT)
                    {
                        throw new InvalidDataException("Disconnect sem marcador.");
                    }
                    int disconnectedId = reader.ReadInt32();
                    Action<int> handler = PlayerDisconnected;
                    if (handler != null)
                    {
                        handler(disconnectedId);
                    }
                }
                else if (packetId == (int)PacketType.CONFIG_SYNC)
                {
                    int marker = reader.ReadInt32();
                    if (marker != (int)PacketType.CONFIG_SYNC)
                    {
                        throw new InvalidDataException("Config sem marcador.");
                    }
                    // Leitura tolerante: confere bytes restantes antes de cada
                    // bool; o que faltar vira false. Ordem canonica:
                    // AllowTeleport, ShareAbilities, ShareStoryOnly,
                    // ShareWorldEvents, ShareDoorsAndLevers, ShowNicknames,
                    // ClientColors, EntitySync.
                    bool[] configFlags = new bool[8];
                    for (int i = 0; i < 8; i++)
                    {
                        if (reader.BaseStream.Length - reader.BaseStream.Position >= 1)
                        {
                            configFlags[i] = reader.ReadBoolean();
                        }
                        else
                        {
                            configFlags[i] = false;
                        }
                    }
                    ConfigSyncHandler handler = ConfigSyncReceived;
                    if (handler != null)
                    {
                        handler(configFlags[0], configFlags[1], configFlags[2], configFlags[3], configFlags[4], configFlags[5], configFlags[6], configFlags[7]);
                    }
                    Action<bool> entityHandler = EntitySyncChanged;
                    if (entityHandler != null)
                    {
                        entityHandler(configFlags[7]);
                    }
                }
                else if (packetId == (int)PacketType.SKILL)
                {
                    // Relay do servidor: marcador 7 + fromId + skill. Sem
                    // consumidor nesta build: valida o framing e descarta
                    // (o SysAck ja foi enviado acima).
                    ExpectMarker(reader, (int)PacketType.SKILL, "SKILL");
                    ExpectRemaining(reader, 8, "SKILL");
                    reader.ReadBytes(8);
                }
                else if (packetId == (int)PacketType.COLOR)
                {
                    // Unicast inicial (marcador 6 + 3 bytes RGB) ou eco
                    // (marcador 6 + fromId + 3 bytes). Sem consumidor:
                    // valida e descarta.
                    ExpectMarker(reader, (int)PacketType.COLOR, "COLOR");
                    ExpectRemaining(reader, 3, "COLOR");
                    long left = reader.BaseStream.Length - reader.BaseStream.Position;
                    reader.ReadBytes((int)left);
                }
                else if (packetId == (int)PacketType.SYNC_ABILITY)
                {
                    ExpectMarker(reader, (int)PacketType.SYNC_ABILITY, "SYNC_ABILITY");
                    ExpectRemaining(reader, 8, "SYNC_ABILITY");
                    reader.ReadBytes(8);
                }
                else if (packetId == (int)PacketType.SYNC_LEVER)
                {
                    ExpectMarker(reader, (int)PacketType.SYNC_LEVER, "SYNC_LEVER");
                    ExpectRemaining(reader, 24, "SYNC_LEVER");
                    reader.ReadBytes(24);
                }
                else if (packetId == (int)PacketType.SYNC_DOOR)
                {
                    ExpectMarker(reader, (int)PacketType.SYNC_DOOR, "SYNC_DOOR");
                    ExpectRemaining(reader, 20, "SYNC_DOOR");
                    reader.ReadBytes(20);
                }
                else if (packetId == (int)PacketType.SYNC_WORLDEVENT)
                {
                    ExpectMarker(reader, (int)PacketType.SYNC_WORLDEVENT, "SYNC_WORLDEVENT");
                    ExpectRemaining(reader, 24, "SYNC_WORLDEVENT");
                    reader.ReadBytes(24);
                }
                else if (packetId == (int)PacketType.SYNC_BREAKABLE)
                {
                    ExpectMarker(reader, (int)PacketType.SYNC_BREAKABLE, "SYNC_BREAKABLE");
                }
            }
        }

        private static void ExpectMarker(BinaryReader reader, int packetId, string name)
        {
            int marker = reader.ReadInt32();
            if (marker != packetId)
            {
                throw new InvalidDataException(name + " sem marcador.");
            }
        }

        private static void ExpectRemaining(BinaryReader reader, int count, string name)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < count)
            {
                throw new InvalidDataException(name + " truncado.");
            }
        }

        private bool IsNewerThanLast(int senderId, uint seq)
        {
            uint last;
            if (_lastRelaySeq.TryGetValue(senderId, out last))
            {
                unchecked
                {
                    uint diff = seq - last;
                    if ((int)diff <= 0)
                    {
                        return false;
                    }
                }
            }
            _lastRelaySeq[senderId] = seq;
            return true;
        }

        private void RaiseSnapshot(PlayerSnapshot snapshot)
        {
            Action<PlayerSnapshot> handler = PlayerSnapshotReceived;
            if (handler != null && snapshot.PlayerId != _assignedId)
            {
                handler(snapshot);
            }
        }

        private static string StripBrackets(string value)
        {
            return (value ?? string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);
        }

        private static void WriteLegacyString(BinaryWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.Write((int)0);
                return;
            }
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadLegacyString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new InvalidDataException("Invalid legacy string length.");
            }
            return System.Text.Encoding.ASCII.GetString(reader.ReadBytes(length));
        }

        public void Dispose()
        {
            // Best-effort: avisa a saida antes de fechar o socket.
            try
            {
                if (_assignedId >= 0)
                {
                    SendDisconnect();
                }
            }
            catch (Exception)
            {
            }
            lock (_sync)
            {
                _running = false;
                try
                {
                    _client.Close();
                }
                catch (Exception)
                {
                }
            }

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join(1000);
            }
        }
    }
}
