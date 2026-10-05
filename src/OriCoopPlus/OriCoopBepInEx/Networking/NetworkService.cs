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
    public sealed class NetworkService : INetworkService
    {
        private const int ChatPacket = -5;
        private const int NetworkVariablePacket = -3;
        private const long PingIntervalTicks = TimeSpan.TicksPerSecond * 2;

        private readonly UdpClient _client;
        private readonly IPEndPoint _server;
        private readonly object _sync = new object();
        private readonly Dictionary<int, uint> _lastRelaySeq = new Dictionary<int, uint>();
        private Thread _receiveThread;
        private bool _running;
        private int _assignedId = -1;
        private uint _sessionToken;
        private uint _sendSeq;
        private string _nickname;
        private string _lastRejectReason = string.Empty;
        private long _lastPingSentTicks;
        private byte _lastSentState = 255;
        private int _lastSentHash;
        private Vector3Data _lastSentPos;
        private long _lastSentTicks;
        private const long HeartbeatIntervalTicks = TimeSpan.TicksPerMillisecond * 400;

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
            _client.Client.ReceiveTimeout = 1000;
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
            _nickname = string.IsNullOrEmpty(nickname) ? "NONICK" : nickname.Trim();
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
                SendSystem((int)PacketType.TELEPORT_REQUEST, body.ToArray(), 0);
            }
        }

        public void SendNicknameUpdate(string newNick)
        {
            if (string.IsNullOrEmpty(newNick))
            {
                return;
            }

            _nickname = newNick.Trim();
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
                    if (_assignedId < 0)
                    {
                        SendHello();
                    }
                    else if (DateTime.UtcNow.Ticks - _lastPingSentTicks >= PingIntervalTicks)
                    {
                        SendPing();
                    }

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
                SendRaw(stream.ToArray());
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
                reader.ReadByte(); // flags
                uint seq = reader.ReadUInt32();
                int headerClientId = reader.ReadInt32();
                reader.ReadUInt32(); // token (servidor nao o ecoa)
                int packetId = reader.ReadInt32();
                reader.ReadUInt32(); // ackSeq
                if (magic != NetProtocol.Magic || version != NetProtocol.Version)
                {
                    return;
                }
                if (packetId == NetProtocol.MsgWelcome)
                {
                    int assignedId = reader.ReadInt32();
                    uint token = reader.ReadUInt32();
                    reader.ReadByte(); // serverVer
                    _assignedId = assignedId;
                    _sessionToken = token;
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
                    byte flags = reader.ReadByte();
                    snapshot.Animation.FacingLeft = (flags & 1) != 0;
                    snapshot.Animation.IsGrounded = (flags & 2) != 0;
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

                else if (packetId == ChatPacket)
                {
                    int marker = reader.ReadInt32();
                    if (marker != ChatPacket)
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
                    try
                    {
                        bool tp = reader.ReadBoolean();
                        bool ab = reader.ReadBoolean();
                        bool story = reader.ReadBoolean();
                        bool world = reader.ReadBoolean();
                        bool doors = reader.ReadBoolean();
                        bool names = reader.ReadBoolean();
                        ConfigSyncHandler handler = ConfigSyncReceived;
                        if (handler != null)
                        {
                            handler(tp, ab, story, world, doors, names);
                        }
                    }
                    catch (EndOfStreamException) { }
                }
                else if (packetId == NetworkVariablePacket)
                {
                    int marker = reader.ReadInt32();
                    if (marker != NetworkVariablePacket)
                    {
                        throw new InvalidDataException("NetVar sem marcador.");
                    }
                    string name = ReadLegacyString(reader);
                    string value = ReadLegacyString(reader);
                    if (string.Equals(name, "ES", StringComparison.OrdinalIgnoreCase))
                    {
                        bool entitySync;
                        if (bool.TryParse(value, out entitySync))
                        {
                            Action<bool> handler = EntitySyncChanged;
                            if (handler != null)
                            {
                                handler(entitySync);
                            }
                        }
                    }
                }
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
            lock (_sync)
            {
                _running = false;
                _client.Close();
            }

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join(1000);
            }
        }
    }
}
