using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OriCoop;
using OriCoopDedicatedServer.Net.Diagnostics;
using OriCoopDedicatedServer.Net.Session;

namespace OriCoopDedicatedServer.Net.Game
{
    /// <summary>
    /// Capacidade de envio que o host (NetServerHost) expoe a camada Game
    /// (D-13): unicast/broadcast confiavel originado no servidor, relay
    /// cru (bytes originais) unreliable/confiavel e chat. Implementado pelo
    /// host; consumido por GameHandlers, DummyBot e comandos.
    /// </summary>
    public interface IGameTransport
    {
        Task UnicastReliableAsync(Session.Session session, int packetId, byte[] payload, CancellationToken ct);

        Task BroadcastReliableAsync(int packetId, byte[] payload, CancellationToken ct);

        Task RelayUnreliableAsync(byte[] originalDatagram, Session.Session sender, CancellationToken ct);

        Task RelayReliableAsync(byte[] originalDatagram, Session.Session sender, uint seq, CancellationToken ct);

        Task ChatUnicastAsync(Session.Session session, string sender, string text, CancellationToken ct);

        Task ChatBroadcastAsync(string sender, string text, CancellationToken ct);

        Task BroadcastStateAsync(byte[] statePayload, CancellationToken ct);
    }

    /// <summary>
    /// Regras de jogo do novo core (D-13/D-15/D-16): instancia injetavel
    /// (SessionManager + ConfigStore + DummyBot + transporte + ILogger),
    /// sem estatico global mutavel. Porta o NetworkHandler antigo:
    /// OnPlayerJoin (COLOR inicial + CONFIG_SYNC unicast + historico de
    /// abilities), TELEPORT_REQUEST (nega unicast sem AllowTeleport, resolve
    /// DummyId 999 e LastKnownPositions), SYNC_* com gating, SKILL, COLOR,
    /// DUMMY_ACTION (so acoes 0/1 do cliente) e limpeza no DISCONNECT.
    /// Leitura defensiva: checagem de length antes de cada campo (T-02-08,
    /// D-03); descarte logado. Corpos no fio mantem layout legado
    /// byte-identico com leading int do packetId.
    /// </summary>
    public sealed class GameHandlers
    {
        private const int ChatPacketId = -5;

        private readonly object _sync = new object();
        private readonly SessionManager _sessions;
        private readonly ConfigStore _config;
        private readonly DummyBot _dummy;
        private readonly IGameTransport _transport;
        private readonly ILogger _log;
        private readonly PlayerStateRelay _relay = new PlayerStateRelay();
        // Gate dedicado do dominio de eventos (PLAYER_EVENT 19): instancia
        // separada do gate de snapshots (nunca o _relay compartilhado).
        // Eventos tem contador proprio no envio (cliente NextEventSeq) e
        // visto-por-remetente proprio aqui — rajadas de tiro nunca suprimem
        // movimento e vice-versa (T-03-03, D-09 classe). Entradas morrem em
        // OnSessionLeft para nao crescer sem limite.
        private readonly EventRelayGate _eventRelay = new EventRelayGate();
        private readonly Dictionary<int, LastRemoteState> _lastKnown = new Dictionary<int, LastRemoteState>();
        private readonly HashSet<int> _unlockedAbilities = new HashSet<int>();

        public struct LastRemoteState
        {
            public float X;
            public float Y;
            public float Z;
            public byte State;
            public byte Flags;
            public int AnimHash;
            public float SpeedX;
            public float SpeedY;
            public DateTime ReceivedAt;
        }

        /// <summary>
        /// Gate unreliable do dominio de eventos (PLAYER_EVENT 19): drop-old
        /// wrap-safe por remetente com visto proprio (dicionario interno),
        /// disjunto do LastRecvSeq da sessao que o PlayerStateRelay usa para
        /// snapshots. Mesma idioma de comparacao ((int)(nova - ultima) > 0).
        /// </summary>
        private sealed class EventRelayGate
        {
            private readonly Dictionary<int, uint> _lastSeen = new Dictionary<int, uint>();
            private readonly object _gateSync = new object();

            public bool ShouldRelay(int senderId, uint seq)
            {
                lock (_gateSync)
                {
                    uint last;
                    if (_lastSeen.TryGetValue(senderId, out last))
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
                    _lastSeen[senderId] = seq;
                    return true;
                }
            }

            public void Forget(int senderId)
            {
                lock (_gateSync)
                {
                    _lastSeen.Remove(senderId);
                }
            }
        }

        public GameHandlers(
            SessionManager sessions,
            ConfigStore config,
            DummyBot dummy,
            IGameTransport transport,
            ILogger log)
        {
            _sessions = sessions ?? throw new ArgumentNullException("sessions");
            _config = config ?? throw new ArgumentNullException("config");
            _dummy = dummy ?? throw new ArgumentNullException("dummy");
            _transport = transport ?? throw new ArgumentNullException("transport");
            _log = log ?? throw new ArgumentNullException("log");
        }

        /// <summary>
        /// Pacotes de jogo com handler dedicado nesta camada (chat -5 e
        /// DISCONNECT 4 continuam no host; PLAYER_STATE 18 entra por
        /// HandlePlayerStateAsync; PLAYER_EVENT 19 entra por
        /// HandlePlayerEventAsync).
        /// </summary>
        public static bool IsGamePacket(int packetId)
        {
            return packetId == (int)PacketType.TELEPORT_REQUEST
                || packetId == (int)PacketType.SYNC_ABILITY
                || packetId == (int)PacketType.SYNC_LEVER
                || packetId == (int)PacketType.SYNC_DOOR
                || packetId == (int)PacketType.SYNC_BREAKABLE
                || packetId == (int)PacketType.SYNC_WORLDEVENT
                || packetId == (int)PacketType.SKILL
                || packetId == (int)PacketType.COLOR
                || packetId == (int)PacketType.CONFIG_SYNC
                || packetId == (int)PacketType.DUMMY_ACTION
                || packetId == (int)PacketType.PLAYER_EVENT;
        }

        // ---- ciclo de sessao (chamado pelo host) ----

        public async Task OnPlayerJoinAsync(Session.Session session, CancellationToken ct)
        {
            // Cor inicial sempre enviada (fallback do servidor). Com
            // ClientColors ligado o cliente pode sobrescrever com a sua via
            // COLOR 6; sem ela o jogador ficava sem cor ate mandar a propria.
            byte[] rgb = _config.GetOrCreateColor(session.Id);
            byte[] colorPayload = new byte[7];
            BinaryPrimitives.WriteInt32LittleEndian(colorPayload.AsSpan(0, 4), (int)PacketType.COLOR);
            colorPayload[4] = rgb[0];
            colorPayload[5] = rgb[1];
            colorPayload[6] = rgb[2];
            await _transport.UnicastReliableAsync(session, (int)PacketType.COLOR, colorPayload, ct).ConfigureAwait(false);

            await _transport.UnicastReliableAsync(session, (int)PacketType.CONFIG_SYNC, _config.BuildConfigPayload(), ct).ConfigureAwait(false);

            if (_config.ShareAbilities)
            {
                int[] history;
                lock (_sync)
                {
                    history = new int[_unlockedAbilities.Count];
                    _unlockedAbilities.CopyTo(history);
                }
                foreach (int abilityId in history)
                {
                    await _transport.UnicastReliableAsync(
                        session, (int)PacketType.SYNC_ABILITY, BuildAbilityPayload(-1, abilityId), ct).ConfigureAwait(false);
                }
                if (history.Length > 0)
                {
                    _log.Log(ServerLogLevel.Info, "GAME",
                        "Sincronizadas " + history.Length + " habilidades ja desbloqueadas para o novo jogador " + session.Id + ".");
                }
            }
        }

        /// <summary>Limpeza de LastKnown (host remove a sessao e avisa).</summary>
        public void OnSessionLeft(int clientId)
        {
            lock (_sync)
            {
                _lastKnown.Remove(clientId);
            }
            _eventRelay.Forget(clientId);
        }

        // ---- dispatch de pacotes de jogo (sessoes ja validadas pelo host) ----

        public Task DispatchAsync(Session.Session sender, uint seq, int packetId, byte[] payload, byte[] originalDatagram, CancellationToken ct)
        {
            switch (packetId)
            {
                case (int)PacketType.TELEPORT_REQUEST:
                    return OnTeleportRequestAsync(sender, payload, ct);
                case (int)PacketType.SYNC_ABILITY:
                    return OnSyncAbilityAsync(sender, seq, payload, originalDatagram, ct);
                case (int)PacketType.SYNC_LEVER:
                    return OnSyncLeverAsync(sender, seq, payload, originalDatagram, ct);
                case (int)PacketType.SYNC_DOOR:
                    return OnSyncDoorAsync(sender, seq, payload, originalDatagram, ct);
                case (int)PacketType.SYNC_WORLDEVENT:
                    return OnSyncWorldEventAsync(sender, seq, payload, originalDatagram, ct);
                case (int)PacketType.SYNC_BREAKABLE:
                    return OnSyncBreakable(sender, payload);
                case (int)PacketType.SKILL:
                    return OnSkillAsync(sender, payload, ct);
                case (int)PacketType.COLOR:
                    return OnColorAsync(sender, payload, ct);
                case (int)PacketType.DUMMY_ACTION:
                    return OnDummyActionAsync(sender, payload);
                case (int)PacketType.PLAYER_EVENT:
                    return HandlePlayerEventAsync(sender, seq, payload, originalDatagram, ct);
                case (int)PacketType.CONFIG_SYNC:
                    _log.Log(ServerLogLevel.Debug, "GAME", "CONFIG_SYNC de " + sender.Id + " ignorado (servidor e autoridade)");
                    return Task.CompletedTask;
                default:
                    return Task.CompletedTask;
            }
        }

        public async Task HandlePlayerStateAsync(Session.Session sender, uint seq, byte[] payload, byte[] originalDatagram, CancellationToken ct)
        {
            float x;
            float y;
            float z;
            byte state;
            byte flags;
            int animHash;
            float speedX;
            float speedY;
            string nick;
            if (!TryParsePlayerState(payload, out x, out y, out z, out state, out flags, out animHash, out speedX, out speedY, out nick))
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "PLAYER_STATE truncado de " + sender.Id + " (drop)");
                return;
            }
            lock (_sync)
            {
                _lastKnown[sender.Id] = new LastRemoteState
                {
                    X = x,
                    Y = y,
                    Z = z,
                    State = state,
                    Flags = flags,
                    AnimHash = animHash,
                    SpeedX = speedX,
                    SpeedY = speedY,
                    ReceivedAt = DateTime.UtcNow,
                };
            }
            if (!_relay.ShouldRelay(sender, seq))
            {
                return;
            }
            await _transport.RelayUnreliableAsync(originalDatagram, sender, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Evento de personagem (fase 3, piloto Spirit Flame, D-09): valida o
        /// corpo minimo de 49 bytes v2 + marcador 19, aplica o gate dedicado de
        /// eventos (drop-old wrap-safe por remetente, dominio disjunto do de
        /// snapshots) e reemite os bytes originais do datagrama sem
        /// reconstrucao — clientId + seq do remetente preservados, sem
        /// carimbo de fromId (identidade viaja no header do envelope, D-11).
        /// </summary>
        public async Task HandlePlayerEventAsync(Session.Session sender, uint seq, byte[] payload, byte[] originalDatagram, CancellationToken ct)
        {
            byte kind;
            float dirX;
            float dirY;
            float dirZ;
            float originX;
            float originY;
            float originZ;
            float aimX;
            float aimY;
            float aimZ;
            long timestampTicks;
            if (!TryParsePlayerEvent(payload, out kind, out dirX, out dirY, out dirZ, out originX, out originY, out originZ, out aimX, out aimY, out aimZ, out timestampTicks))
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "PLAYER_EVENT truncado de " + sender.Id + " (drop)");
                return;
            }
            if (!_eventRelay.ShouldRelay(sender.Id, seq))
            {
                return;
            }
            await _transport.RelayUnreliableAsync(originalDatagram, sender, ct).ConfigureAwait(false);
        }

        private Task OnTeleportRequestAsync(Session.Session sender, byte[] payload, CancellationToken ct)
        {
            if (!_config.AllowTeleport)
            {
                // Nega so ao solicitante (nunca broadcast).
                return _transport.ChatUnicastAsync(sender,
                    "<color=red>SERVER</color>",
                    "<color=yellow>Teleporte desativado pelo servidor. Use /coop tp on.</color>", ct);
            }
            if (payload == null || payload.Length < 8
                || BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.TELEPORT_REQUEST)
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "TELEPORT_REQUEST truncado de " + sender.Id + " (drop)");
                return Task.CompletedTask;
            }
            int targetId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));

            float tx;
            float ty;
            float tz;
            string targetNick;
            if (_dummy.IsActive && targetId == DummyBot.DummyId && _dummy.GetPosition(out tx, out ty, out tz))
            {
                targetNick = DummyBot.DummyNick;
            }
            else
            {
                Session.Session? target;
                LastRemoteState known;
                bool hasTarget = _sessions.TryGet(targetId, out target) && target != null && target.IsReady;
                lock (_sync)
                {
                    if (!hasTarget || target == null || !_lastKnown.TryGetValue(targetId, out known))
                    {
                        return _transport.ChatUnicastAsync(sender,
                            "<color=red>SERVER</color>",
                            "<color=yellow>Destino de teleporte indisponivel (sem snapshots recentes).</color>", ct);
                    }
                    tx = known.X;
                    ty = known.Y;
                    tz = known.Z;
                }
                targetNick = target.Nickname;
                if (string.IsNullOrEmpty(targetNick))
                {
                    targetNick = "Player " + target.Id;
                }
            }

            string safeNick = StripBrackets(sender.Nickname);
            if (string.IsNullOrEmpty(safeNick))
            {
                safeNick = "Player " + sender.Id;
            }
            Task reply = _transport.UnicastReliableAsync(
                sender, (int)PacketType.TELEPORT_REQUEST, BuildTeleportResponse(tx, ty, tz, targetNick), ct);
            Task announce = _transport.ChatBroadcastAsync(
                "<color=red>SERVER</color>",
                "<color=cyan>" + safeNick + "</color> foi teleportado ate <color=cyan>" + targetNick + "</color>.", ct);
            return Task.WhenAll(reply, announce);
        }

        private Task OnSkillAsync(Session.Session sender, byte[] payload, CancellationToken ct)
        {
            if (payload == null || payload.Length < 8
                || BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.SKILL)
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "SKILL truncado de " + sender.Id + " (drop)");
                return Task.CompletedTask;
            }
            int skill = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
            byte[] relay = new byte[12];
            BinaryPrimitives.WriteInt32LittleEndian(relay.AsSpan(0, 4), (int)PacketType.SKILL);
            BinaryPrimitives.WriteInt32LittleEndian(relay.AsSpan(4, 4), sender.Id);
            BinaryPrimitives.WriteInt32LittleEndian(relay.AsSpan(8, 4), skill);
            // SKILL e critico (D-10): confiavel com retry via host.
            return _transport.BroadcastReliableAsync((int)PacketType.SKILL, relay, ct);
        }

        private Task OnColorAsync(Session.Session sender, byte[] payload, CancellationToken ct)
        {
            if (!_config.ClientColors)
            {
                return Task.CompletedTask;
            }
            if (payload == null || payload.Length < 7
                || BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.COLOR)
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "COLOR truncado de " + sender.Id + " (drop)");
                return Task.CompletedTask;
            }
            byte r = payload[4];
            byte g = payload[5];
            byte b = payload[6];
            _log.Log(ServerLogLevel.Info, "GAME", "[" + sender.Id + "] cor customizada: R:" + r + " G:" + g + " B:" + b);
            _config.SetColor(sender.Id, r, g, b);
            byte[] echo = new byte[11];
            BinaryPrimitives.WriteInt32LittleEndian(echo.AsSpan(0, 4), (int)PacketType.COLOR);
            BinaryPrimitives.WriteInt32LittleEndian(echo.AsSpan(4, 4), sender.Id);
            echo[8] = r;
            echo[9] = g;
            echo[10] = b;
            return _transport.UnicastReliableAsync(sender, (int)PacketType.COLOR, echo, ct);
        }

        private Task OnSyncAbilityAsync(Session.Session sender, uint seq, byte[] payload, byte[] originalDatagram, CancellationToken ct)
        {
            if (payload == null || payload.Length < 8
                || BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.SYNC_ABILITY)
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "SYNC_ABILITY truncado de " + sender.Id + " (drop)");
                return Task.CompletedTask;
            }
            int abilityId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
            _log.Log(ServerLogLevel.Info, "GAME", "[" + sender.Id + "] HABILIDADE DESBLOQUEADA: " + abilityId);
            if (!_config.ShareAbilities)
            {
                return Task.CompletedTask;
            }
            lock (_sync)
            {
                _unlockedAbilities.Add(abilityId);
            }
            byte[] relay = BuildAbilityPayload(sender.Id, abilityId);
            return _transport.BroadcastReliableAsync((int)PacketType.SYNC_ABILITY, relay, ct);
        }

        private Task OnSyncLeverAsync(Session.Session sender, uint seq, byte[] payload, byte[] originalDatagram, CancellationToken ct)
        {
            int[] fields;
            if (!TryReadInts(payload, (int)PacketType.SYNC_LEVER, 5, out fields))
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "SYNC_LEVER truncado de " + sender.Id + " (drop)");
                return Task.CompletedTask;
            }
            if (!_config.ShareDoorsAndLevers)
            {
                return Task.CompletedTask;
            }
            return _transport.BroadcastReliableAsync(
                (int)PacketType.SYNC_LEVER, BuildStampedPayload((int)PacketType.SYNC_LEVER, sender.Id, fields), ct);
        }

        private Task OnSyncDoorAsync(Session.Session sender, uint seq, byte[] payload, byte[] originalDatagram, CancellationToken ct)
        {
            int[] fields;
            if (!TryReadInts(payload, (int)PacketType.SYNC_DOOR, 4, out fields))
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "SYNC_DOOR truncado de " + sender.Id + " (drop)");
                return Task.CompletedTask;
            }
            if (!_config.ShareDoorsAndLevers)
            {
                return Task.CompletedTask;
            }
            return _transport.BroadcastReliableAsync(
                (int)PacketType.SYNC_DOOR, BuildStampedPayload((int)PacketType.SYNC_DOOR, sender.Id, fields), ct);
        }

        private Task OnSyncWorldEventAsync(Session.Session sender, uint seq, byte[] payload, byte[] originalDatagram, CancellationToken ct)
        {
            int[] fields;
            if (!TryReadInts(payload, (int)PacketType.SYNC_WORLDEVENT, 5, out fields))
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "SYNC_WORLDEVENT truncado de " + sender.Id + " (drop)");
                return Task.CompletedTask;
            }
            if (!_config.ShareWorldEvents)
            {
                return Task.CompletedTask;
            }
            return _transport.BroadcastReliableAsync(
                (int)PacketType.SYNC_WORLDEVENT, BuildStampedPayload((int)PacketType.SYNC_WORLDEVENT, sender.Id, fields), ct);
        }

        private Task OnSyncBreakable(Session.Session sender, byte[] payload)
        {
            // Sem sender nem gating no jogo atual (o handler antigo tambem
            // descartava): valida o marcador e registra para diagnostico.
            if (payload == null || payload.Length < 4
                || BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.SYNC_BREAKABLE)
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "SYNC_BREAKABLE truncado de " + sender.Id + " (drop)");
            }
            else
            {
                _log.Log(ServerLogLevel.Debug, "GAME", "SYNC_BREAKABLE de " + sender.Id + " sem relay (sem sender/gating; a confirmar)");
            }
            return Task.CompletedTask;
        }

        private Task OnDummyActionAsync(Session.Session sender, byte[] payload)
        {
            // Server-local (D-15): do cliente so 0 (toggle) e 1 (ability);
            // lever/door sao console-only e nunca chegam pelo fio.
            if (payload == null || payload.Length < 8
                || BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.DUMMY_ACTION)
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "DUMMY_ACTION truncado de " + sender.Id + " (drop)");
                return Task.CompletedTask;
            }
            int action = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4, 4));
            if (action == 0)
            {
                _dummy.Toggle();
            }
            else if (action == 1)
            {
                if (payload.Length < 12)
                {
                    _log.Log(ServerLogLevel.Warning, "GAME", "DUMMY_ACTION ability sem abilityId de " + sender.Id + " (drop)");
                    return Task.CompletedTask;
                }
                int abilityId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(8, 4));
                _dummy.TriggerAbility(abilityId, AbilityName(abilityId));
            }
            else
            {
                _log.Log(ServerLogLevel.Warning, "GAME", "DUMMY_ACTION " + action + " de " + sender.Id + " recusada (so 0/1 via fio)");
            }
            return Task.CompletedTask;
        }

        // ---- API para comandos (via IServerContext.Game) ----

        /// <summary>Busca sessao pronta por nick exato (CI) ou ID.</summary>
        public Session.Session? FindSession(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return null;
            }
            int id;
            if (int.TryParse(query, out id))
            {
                Session.Session byId;
                if (_sessions.TryGet(id, out byId) && byId != null && byId.IsReady)
                {
                    return byId;
                }
            }
            foreach (Session.Session candidate in _sessions.All)
            {
                if (candidate != null && candidate.IsReady
                    && string.Equals(candidate.Nickname, query, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        public bool TryGetPosition(int clientId, out float x, out float y, out float z)
        {
            lock (_sync)
            {
                LastRemoteState known;
                if (_lastKnown.TryGetValue(clientId, out known))
                {
                    x = known.X;
                    y = known.Y;
                    z = known.Z;
                    return true;
                }
            }
            x = 0f;
            y = 0f;
            z = 0f;
            return false;
        }

        public DummyPlayerView? GetPrimaryPlayerView()
        {
            lock (_sync)
            {
                bool any = false;
                LastRemoteState freshest = default(LastRemoteState);
                foreach (KeyValuePair<int, LastRemoteState> pair in _lastKnown)
                {
                    if (pair.Key == DummyBot.DummyId)
                    {
                        continue;
                    }
                    if (!any || pair.Value.ReceivedAt > freshest.ReceivedAt)
                    {
                        freshest = pair.Value;
                        any = true;
                    }
                }
                if (!any)
                {
                    return null;
                }
                return new DummyPlayerView
                {
                    X = freshest.X,
                    Y = freshest.Y,
                    Z = freshest.Z,
                    State = freshest.State,
                    Flags = freshest.Flags,
                    AnimHash = freshest.AnimHash,
                    SpeedX = freshest.SpeedX,
                    SpeedY = freshest.SpeedY,
                    ReceivedAt = freshest.ReceivedAt,
                };
            }
        }

        public Task SendTeleportAsync(Session.Session source, float x, float y, float z, string destNick, CancellationToken ct)
        {
            return _transport.UnicastReliableAsync(
                source, (int)PacketType.TELEPORT_REQUEST, BuildTeleportResponse(x, y, z, destNick), ct);
        }

        public Task ChatToAsync(Session.Session session, string sender, string text, CancellationToken ct)
        {
            return _transport.ChatUnicastAsync(session, sender, text, ct);
        }

        public Task ChatAllAsync(string sender, string text, CancellationToken ct)
        {
            return _transport.ChatBroadcastAsync(sender, text, ct);
        }

        // ---- codecs (layout legado byte-identico, leading int = packetId) ----

        /// <summary>
        /// Snapshot servidor→cliente NO formato que o cliente new-core le:
        /// marcador 18 + pos + state + flags + animHash + speeds + nick, SEM
        /// carimbo de ID no corpo (a identidade viaja no clientId do header
        /// do envelope — 999 para o dummy). O carimbo legado [18][id]... foi
        /// abandonado aqui porque o parser atual o leria como posicao.
        /// </summary>
        public static byte[] BuildPlayerStatePayload(
            float x, float y, float z, byte state, byte flags,
            int animHash, float speedX, float speedY, string nick)
        {
            byte[] nickBytes = Encoding.ASCII.GetBytes(nick ?? string.Empty);
            byte[] payload = new byte[4 + 12 + 1 + 1 + 4 + 4 + 4 + 4 + nickBytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.PLAYER_STATE);
            WriteFloat(payload, 4, x);
            WriteFloat(payload, 8, y);
            WriteFloat(payload, 12, z);
            payload[16] = state;
            payload[17] = flags;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(18, 4), animHash);
            WriteFloat(payload, 22, speedX);
            WriteFloat(payload, 26, speedY);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(30, 4), nickBytes.Length);
            Buffer.BlockCopy(nickBytes, 0, payload, 34, nickBytes.Length);
            return payload;
        }

        /// <summary>
        /// Corpo cliente→servidor: marcador 18 + pos(12) + state(1) +
        /// flags(1) + animHash(4) + speedX(4) + speedY(4) + nick legada
        /// (sem clientId: o remetente vem do header do envelope).
        /// </summary>
        public static bool TryParsePlayerState(
            byte[] payload, out float x, out float y, out float z, out byte state,
            out byte flags, out int animHash, out float speedX, out float speedY, out string nick)
        {
            x = 0f;
            y = 0f;
            z = 0f;
            state = 0;
            flags = 0;
            animHash = 0;
            speedX = 0f;
            speedY = 0f;
            nick = string.Empty;
            if (payload == null || payload.Length < 34)
            {
                return false;
            }
            if (BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.PLAYER_STATE)
            {
                return false;
            }
            x = ReadFloat(payload, 4);
            y = ReadFloat(payload, 8);
            z = ReadFloat(payload, 12);
            state = payload[16];
            flags = payload[17];
            animHash = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(18, 4));
            speedX = ReadFloat(payload, 22);
            speedY = ReadFloat(payload, 26);
            int nickLen = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(30, 4));
            if (nickLen < 0 || nickLen > payload.Length - 34)
            {
                return false;
            }
            try
            {
                nick = Encoding.ASCII.GetString(payload, 34, nickLen);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Corpo PLAYER_EVENT 19 (ordem congelada, 49 bytes v2, D-11):
        /// marcador 19 + kind byte + dir(3 floats) + origin(3 floats) +
        /// aim(3 floats, v2) + timestamp long. Identidade NUNCA no corpo
        /// (clientId do header). Espelho de construcao para testes
        /// (SmokeProbe round-trip). v2 quebra o fio one-way: 37B antigos
        /// sao descartados pelo MinBodyLength (mesmo build, regra D-10).
        /// </summary>
        public static byte[] BuildPlayerEventPayload(
            byte kind, float dirX, float dirY, float dirZ,
            float originX, float originY, float originZ,
            float aimX, float aimY, float aimZ, long timestampTicks)
        {
            byte[] payload = new byte[PlayerEventProtocol.BodySize];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.PLAYER_EVENT);
            payload[PlayerEventProtocol.OffKind] = kind;
            WriteFloat(payload, PlayerEventProtocol.OffDirection, dirX);
            WriteFloat(payload, PlayerEventProtocol.OffDirection + 4, dirY);
            WriteFloat(payload, PlayerEventProtocol.OffDirection + 8, dirZ);
            WriteFloat(payload, PlayerEventProtocol.OffOrigin, originX);
            WriteFloat(payload, PlayerEventProtocol.OffOrigin + 4, originY);
            WriteFloat(payload, PlayerEventProtocol.OffOrigin + 8, originZ);
            WriteFloat(payload, PlayerEventProtocol.OffAim, aimX);
            WriteFloat(payload, PlayerEventProtocol.OffAim + 4, aimY);
            WriteFloat(payload, PlayerEventProtocol.OffAim + 8, aimZ);
            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(PlayerEventProtocol.OffTimestamp, 8), timestampTicks);
            return payload;
        }

        public static bool TryParsePlayerEvent(
            byte[] payload, out byte kind, out float dirX, out float dirY, out float dirZ,
            out float originX, out float originY, out float originZ,
            out float aimX, out float aimY, out float aimZ, out long timestampTicks)
        {
            kind = 0;
            dirX = 0f;
            dirY = 0f;
            dirZ = 0f;
            originX = 0f;
            originY = 0f;
            originZ = 0f;
            aimX = 0f;
            aimY = 0f;
            aimZ = 0f;
            timestampTicks = 0L;
            if (payload == null || payload.Length < PlayerEventProtocol.MinBodyLength)
            {
                return false;
            }
            if (BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.PLAYER_EVENT)
            {
                return false;
            }
            kind = payload[PlayerEventProtocol.OffKind];
            dirX = ReadFloat(payload, PlayerEventProtocol.OffDirection);
            dirY = ReadFloat(payload, PlayerEventProtocol.OffDirection + 4);
            dirZ = ReadFloat(payload, PlayerEventProtocol.OffDirection + 8);
            originX = ReadFloat(payload, PlayerEventProtocol.OffOrigin);
            originY = ReadFloat(payload, PlayerEventProtocol.OffOrigin + 4);
            originZ = ReadFloat(payload, PlayerEventProtocol.OffOrigin + 8);
            aimX = ReadFloat(payload, PlayerEventProtocol.OffAim);
            aimY = ReadFloat(payload, PlayerEventProtocol.OffAim + 4);
            aimZ = ReadFloat(payload, PlayerEventProtocol.OffAim + 8);
            timestampTicks = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(PlayerEventProtocol.OffTimestamp, 8));
            return true;
        }

        public static byte[] BuildTeleportResponse(float x, float y, float z, string destNick)
        {
            byte[] nickBytes = Encoding.ASCII.GetBytes(destNick ?? string.Empty);
            byte[] payload = new byte[4 + 12 + 4 + nickBytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.TELEPORT_REQUEST);
            WriteFloat(payload, 4, x);
            WriteFloat(payload, 8, y);
            WriteFloat(payload, 12, z);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(16, 4), nickBytes.Length);
            Buffer.BlockCopy(nickBytes, 0, payload, 20, nickBytes.Length);
            return payload;
        }

        public static byte[] BuildAbilityPayload(int fromId, int abilityId)
        {
            byte[] payload = new byte[12];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.SYNC_ABILITY);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), fromId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8, 4), abilityId);
            return payload;
        }

        public static byte[] BuildStampedPayload(int packetId, int fromId, int[] fields)
        {
            byte[] payload = new byte[4 + 4 + (fields.Length * 4)];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), packetId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), fromId);
            for (int i = 0; i < fields.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8 + (i * 4), 4), fields[i]);
            }
            return payload;
        }

        public static bool TryReadInts(byte[] payload, int packetId, int count, out int[] fields)
        {
            fields = new int[0];
            if (payload == null || payload.Length < 4 + (count * 4))
            {
                return false;
            }
            if (BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != packetId)
            {
                return false;
            }
            fields = new int[count];
            for (int i = 0; i < count; i++)
            {
                fields[i] = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4 + (i * 4), 4));
            }
            return true;
        }

        public static byte[] BuildChatPayload(string sender, string text)
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

        private static float ReadFloat(byte[] buffer, int offset)
        {
            return BitConverter.ToSingle(buffer, offset);
        }

        private static void WriteFloat(byte[] buffer, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            buffer[offset] = bytes[0];
            buffer[offset + 1] = bytes[1];
            buffer[offset + 2] = bytes[2];
            buffer[offset + 3] = bytes[3];
        }

        private static string StripBrackets(string value)
        {
            return (value ?? string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);
        }

        private static string AbilityName(int id)
        {
            switch (id)
            {
                case 0: return "Bash";
                case 2: return "ChargeFlame";
                case 3: return "WallJump";
                case 4: return "Stomp";
                case 5: return "DoubleJump";
                case 8: return "ChargeJump";
                case 10: return "Magnet";
                case 12: return "Climb";
                case 14: return "Glide";
                case 15: return "SpiritFlame";
                case 23: return "WaterBreath";
                case 50: return "Dash";
                case 51: return "Grenade";
                case 53: return "ChargeDash";
                case 54: return "AirDash";
                default: return "Habilidade_" + id;
            }
        }
    }
}
