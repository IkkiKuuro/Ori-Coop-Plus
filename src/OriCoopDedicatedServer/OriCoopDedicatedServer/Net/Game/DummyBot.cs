using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading;
using OriCoop;
using OriCoopDedicatedServer.Net.Diagnostics;

namespace OriCoopDedicatedServer.Net.Game
{
    /// <summary>
    /// Visao minima de um jogador para o dummy (eco + ancora): floats e bytes
    /// do ultimo snapshot, sem referencia a sessoes.
    /// </summary>
    public struct DummyPlayerView
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
    /// Bot de teste server-local (D-15): instancia injetavel com ILogger, sem
    /// estatico global mutavel. ID 999 fora do allocator (nunca alocado pelo
    /// SessionManager). Transmite PLAYER_STATE 18 com clientId 999 no header
    /// do envelope. Modos: eco (espelho com jitter 20-150 ms, padrao), ciclo
    /// de 12 anims e parado. Tick de 100 ms via Timer; TriggerAbility/Lever/
    /// Door sao console-only (nunca via fio — DUMMY_ACTION 17 do cliente so
    /// aceita acoes 0/1, ver GameHandlers).
    /// </summary>
    public sealed class DummyBot
    {
        public const int DummyId = 999;
        public const string DummyNick = "Bot_Amigo";

        private static readonly byte[] DummyColor = new byte[] { 50, 220, 255 };

        private static readonly ActionVisualState[] AnimSequence = new ActionVisualState[]
        {
            ActionVisualState.Idle,
            ActionVisualState.Running,
            ActionVisualState.Jump,
            ActionVisualState.DoubleJump,
            ActionVisualState.Falling,
            ActionVisualState.WallSlide,
            ActionVisualState.WallJump,
            ActionVisualState.Bash,
            ActionVisualState.Glide,
            ActionVisualState.ChargeJump,
            ActionVisualState.Stomp,
            ActionVisualState.Dash,
        };

        private const int AnimStateDurationTicks = 25; // 2.5 s por estado (tick 100 ms)
        private const int EchoMinDelayMs = 20;
        private const int EchoMaxDelayMs = 150;
        private const float EchoOffsetX = 1.5f;
        private const double EchoFreshnessSeconds = 2.0;

        private readonly object _sync = new object();
        private readonly ILogger _log;
        private readonly IGameTransport _transport;
        private readonly ConfigStore _config;
        private readonly Random _jitterRand = new Random();
        private readonly Queue<EchoFrame> _echoBuffer = new Queue<EchoFrame>();

        private Timer? _loopTimer;
        private bool _isActive;
        private float _posX;
        private float _posY;
        private float _posZ;
        private bool _echoEnabled = true;
        private bool _animCycleEnabled;
        private int _animIndex;
        private int _animTickCount;
        private int _lockedAnimIndex = -1;
        private float _anchorX;
        private float _anchorY;
        private float _anchorZ;
        private float _patrolDir = 1f;
        private bool _faceLeft;
        private float _timeCounter;

        private struct EchoFrame
        {
            public long DueTicks;
            public float X;
            public float Y;
            public float Z;
            public byte State;
            public byte Flags;
            public int AnimHash;
            public float SpeedX;
            public float SpeedY;
        }

        /// <summary>
        /// Provedor do snapshot mais fresco (ligado pelo ServerBoot aos
        /// LastKnownStates do GameHandlers); null = sem jogadores.
        /// </summary>
        public Func<DummyPlayerView?>? PrimaryPlayerProvider { get; set; }

        public DummyBot(ILogger log, IGameTransport transport, ConfigStore config)
        {
            _log = log ?? throw new ArgumentNullException("log");
            _transport = transport ?? throw new ArgumentNullException("transport");
            _config = config ?? throw new ArgumentNullException("config");
        }

        public bool IsActive
        {
            get { lock (_sync) { return _isActive; } }
        }

        public bool EchoEnabled
        {
            get { lock (_sync) { return _echoEnabled; } }
        }

        public bool AnimCycleEnabled
        {
            get { lock (_sync) { return _animCycleEnabled; } }
        }

        public string CurrentModeName()
        {
            lock (_sync)
            {
                if (_animCycleEnabled)
                {
                    return "ciclo-anim (" + CurrentAnimStateNameLocked() + ")";
                }
                if (_echoEnabled)
                {
                    return "eco (espelho com ping 20-150 ms)";
                }
                return "parado (Idle)";
            }
        }

        public string StatusLine()
        {
            lock (_sync)
            {
                return "Dummy Active: " + _isActive
                    + ", Pos: (" + _posX.ToString("F1") + ", " + _posY.ToString("F1") + ", " + _posZ.ToString("F1") + ")"
                    + ", Modo: " + (_animCycleEnabled ? "ciclo-anim (" + CurrentAnimStateNameLocked() + ")"
                        : _echoEnabled ? "eco (espelho com ping 20-150 ms)" : "parado (Idle)");
            }
        }

        public void Toggle()
        {
            if (IsActive)
            {
                Despawn();
            }
            else
            {
                Spawn();
            }
        }

        public void Spawn()
        {
            lock (_sync)
            {
                if (_isActive)
                {
                    return;
                }
                _isActive = true;
                DummyPlayerView? player = GetPrimaryPlayer();
                float baseX = player.HasValue ? player.Value.X : 0f;
                float baseY = player.HasValue ? player.Value.Y : 0f;
                float baseZ = player.HasValue ? player.Value.Z : 0f;
                _posX = baseX + 2.5f;
                _posY = baseY;
                _posZ = baseZ;
                _anchorX = _posX;
                _anchorY = _posY;
                _anchorZ = _posZ;
                _echoEnabled = true;
                _animCycleEnabled = false;
                _lockedAnimIndex = -1;
                _loopTimer = new Timer(OnTick, null, 100, 100);
            }
            _config.SetColor(DummyId, DummyColor[0], DummyColor[1], DummyColor[2]);
            _log.Log(ServerLogLevel.Info, "DUMMY", "[+] Dummy bot '" + DummyNick + "' (ID: " + DummyId + ") SPAWNED!");
            Say("<color=cyan>[+] Test Dummy '" + DummyNick + "' entrou na partida!</color>");
        }

        public void Despawn()
        {
            lock (_sync)
            {
                if (!_isActive)
                {
                    return;
                }
                _isActive = false;
                _animCycleEnabled = false;
                _lockedAnimIndex = -1;
                if (_loopTimer != null)
                {
                    _loopTimer.Dispose();
                    _loopTimer = null;
                }
            }
            byte[] payload = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.DISCONNECT);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), DummyId);
            _ = _transport.BroadcastReliableAsync((int)PacketType.DISCONNECT, payload, CancellationToken.None);
            _log.Log(ServerLogLevel.Info, "DUMMY", "[-] Dummy bot '" + DummyNick + "' (ID: " + DummyId + ") DESPAWNED!");
            Say("<color=red>[-] Test Dummy '" + DummyNick + "' saiu da partida!</color>");
        }

        /// <summary>Para o tick sem aviso (shutdown do host).</summary>
        public void Stop()
        {
            lock (_sync)
            {
                _isActive = false;
                if (_loopTimer != null)
                {
                    _loopTimer.Dispose();
                    _loopTimer = null;
                }
            }
        }

        public void SetEchoMode(bool enabled)
        {
            if (!IsActive)
            {
                Spawn();
            }
            lock (_sync)
            {
                _echoEnabled = enabled;
                if (enabled)
                {
                    _animCycleEnabled = false;
                    _lockedAnimIndex = -1;
                }
                _echoBuffer.Clear();
            }
            _log.Log(ServerLogLevel.Info, "DUMMY",
                "Modo eco: " + (enabled ? "ATIVADO (espelha jogador, ping 20-150 ms)" : "DESATIVADO"));
            Say("<color=yellow>[Dummy Bot]:</color> Modo eco " + (enabled ? "<b>ativado</b>!" : "desativado."));
        }

        public void SetAnimMode(bool enabled)
        {
            if (!IsActive)
            {
                Spawn();
            }
            lock (_sync)
            {
                _animCycleEnabled = enabled;
                _lockedAnimIndex = -1;
                _animIndex = 0;
                _animTickCount = 0;
                if (enabled)
                {
                    _echoEnabled = false;
                }
            }
            _log.Log(ServerLogLevel.Info, "DUMMY",
                "Modo espelho de anims: " + (enabled ? "ATIVADO (ciclo 12 estados)" : "DESATIVADO"));
            Say("<color=yellow>[Dummy Bot]:</color> Modo espelho " + (enabled ? "<b>ativado</b> — ciclo de anims!" : "desativado."));
        }

        public bool LockAnimState(string name)
        {
            if (!IsActive)
            {
                Spawn();
            }
            int found = -1;
            for (int i = 0; i < AnimSequence.Length; i++)
            {
                if (string.Equals(AnimSequence[i].ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    found = i;
                    break;
                }
            }
            if (found < 0)
            {
                return false;
            }
            lock (_sync)
            {
                _animCycleEnabled = true;
                _echoEnabled = false;
                _lockedAnimIndex = found;
                _animIndex = found;
                _animTickCount = 0;
            }
            _log.Log(ServerLogLevel.Info, "DUMMY", "Modo espelho travado em: " + AnimSequence[found]);
            Say("<color=yellow>[Dummy Bot]:</color> Anim travada em <b>" + AnimSequence[found] + "</b>!");
            return true;
        }

        /// <summary>Console-only: simula desbloqueio de habilidade pelo dummy.</summary>
        public void TriggerAbility(int abilityId, string abilityName)
        {
            if (!IsActive)
            {
                Spawn();
            }
            byte[] payload = new byte[12];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.SYNC_ABILITY);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), DummyId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8, 4), abilityId);
            _ = _transport.BroadcastReliableAsync((int)PacketType.SYNC_ABILITY, payload, CancellationToken.None);
            _log.Log(ServerLogLevel.Info, "DUMMY", "[DUMMY] Enviado desbloqueio de habilidade: " + abilityName + " (" + abilityId + ")!");
            Say("<color=yellow>[Dummy Bot]:</color> Habilidade simulada: <b>" + abilityName + "</b>!");
        }

        /// <summary>Console-only: simula acionamento de alavanca pelo dummy.</summary>
        public void TriggerLever(int direction)
        {
            if (!IsActive)
            {
                Spawn();
            }
            byte[] payload = new byte[28];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.SYNC_LEVER);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), DummyId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8, 4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12, 4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(16, 4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(20, 4), 1);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(24, 4), direction);
            _ = _transport.BroadcastReliableAsync((int)PacketType.SYNC_LEVER, payload, CancellationToken.None);
            _log.Log(ServerLogLevel.Info, "DUMMY", "[DUMMY] Enviado teste de alavanca (direcao: " + direction + ")!");
            Say("<color=yellow>[Dummy Bot]:</color> Acionou alavanca (dir: " + direction + ")!");
        }

        /// <summary>Console-only: simula abertura de porta pelo dummy.</summary>
        public void TriggerDoor()
        {
            if (!IsActive)
            {
                Spawn();
            }
            byte[] payload = new byte[24];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.SYNC_DOOR);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), DummyId);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8, 4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12, 4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(16, 4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(20, 4), 1);
            _ = _transport.BroadcastReliableAsync((int)PacketType.SYNC_DOOR, payload, CancellationToken.None);
            _log.Log(ServerLogLevel.Info, "DUMMY", "[DUMMY] Enviado teste de porta keystone!");
            Say("<color=yellow>[Dummy Bot]:</color> Abriu porta de teste!");
        }

        private const string ServerSender = "<color=red>SERVER</color>";

        private void Say(string text)
        {
            try
            {
                _ = _transport.ChatBroadcastAsync(ServerSender, text, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "DUMMY", "Anuncio falhou: " + ex.GetType().Name);
            }
        }

        public bool GetPosition(out float x, out float y, out float z)
        {
            lock (_sync)
            {
                x = _posX;
                y = _posY;
                z = _posZ;
                return _isActive;
            }
        }

        private DummyPlayerView? GetPrimaryPlayer()
        {
            try
            {
                if (PrimaryPlayerProvider != null)
                {
                    return PrimaryPlayerProvider();
                }
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "DUMMY", "Provedor de jogador falhou: " + ex.GetType().Name);
            }
            return null;
        }

        private void OnTick(object? state)
        {
            byte[] payload;
            lock (_sync)
            {
                if (!_isActive)
                {
                    return;
                }
                try
                {
                    _timeCounter += 0.1f;
                    byte visualState = (byte)ActionVisualState.Idle;
                    float speedX = 0f;
                    float speedY = 0f;
                    bool grounded = true;
                    int animHash = 0;

                    if (_animCycleEnabled)
                    {
                        if (_lockedAnimIndex < 0)
                        {
                            _animTickCount++;
                            if (_animTickCount >= AnimStateDurationTicks)
                            {
                                _animTickCount = 0;
                                _animIndex = (_animIndex + 1) % AnimSequence.Length;
                                _log.Log(ServerLogLevel.Info, "DUMMY",
                                    "[DUMMY-ANIM] estado=" + AnimSequence[_animIndex] + " (" + (_animIndex + 1) + "/" + AnimSequence.Length + ")");
                            }
                        }
                        ActionVisualState current = AnimSequence[_animIndex];
                        visualState = (byte)current;
                        GetMirrorVelocity(current, out speedX, out speedY, out grounded);
                        float newX = _posX + speedX * 0.1f;
                        if (newX > _anchorX + 4f || newX < _anchorX - 4f)
                        {
                            _patrolDir = -_patrolDir;
                            newX = _posX + speedX * 0.1f * -1f;
                        }
                        if (speedX > 0.1f)
                        {
                            _faceLeft = false;
                        }
                        else if (speedX < -0.1f)
                        {
                            _faceLeft = true;
                        }
                        float newY = _anchorY + (float)Math.Sin(_timeCounter * 3.0f) * 0.3f;
                        _posX = newX;
                        _posY = newY;
                        _posZ = _anchorZ;
                    }
                    else if (_echoEnabled && TryDequeueEchoLocked(out visualState, out speedX, out speedY, out grounded, out animHash))
                    {
                        // Eco: pose/facing/vel do jogador com atraso; _pos ja atualizado.
                    }
                    else
                    {
                        DummyPlayerView? player = GetPrimaryPlayer();
                        float baseX = player.HasValue ? player.Value.X : 0f;
                        float baseY = player.HasValue ? player.Value.Y : 0f;
                        float baseZ = player.HasValue ? player.Value.Z : 0f;
                        float offsetX = 2.5f + (float)Math.Sin(_timeCounter * 1.5f) * 1.0f;
                        float offsetY = (float)Math.Sin(_timeCounter * 3.0f) * 0.3f;
                        _posX = baseX + offsetX;
                        _posY = baseY + offsetY;
                        _posZ = baseZ;
                        _faceLeft = offsetX < 0;
                    }

                    byte flags = 0;
                    if (_faceLeft)
                    {
                        flags |= 1;
                    }
                    if (grounded)
                    {
                        flags |= 2;
                    }
                    payload = GameHandlers.BuildPlayerStatePayload(
                        _posX, _posY, _posZ, visualState, flags, animHash, speedX, speedY, DummyNick);
                }
                catch (Exception ex)
                {
                    _log.Log(ServerLogLevel.Error, "DUMMY", "Tick error: " + ex.Message);
                    return;
                }
            }
            try
            {
                _transport.BroadcastStateAsync(payload, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "DUMMY", "Broadcast do snapshot falhou: " + ex.GetType().Name);
            }
        }

        private bool TryDequeueEchoLocked(out byte visualState, out float speedX, out float speedY, out bool grounded, out int animHash)
        {
            visualState = (byte)ActionVisualState.Idle;
            speedX = 0f;
            speedY = 0f;
            grounded = true;
            animHash = 0;
            DummyPlayerView? player = GetPrimaryPlayer();
            if (!player.HasValue || (DateTime.UtcNow - player.Value.ReceivedAt).TotalSeconds > EchoFreshnessSeconds)
            {
                return false;
            }
            DummyPlayerView st = player.Value;
            long due = DateTime.UtcNow.Ticks
                + _jitterRand.Next(EchoMinDelayMs, EchoMaxDelayMs + 1) * TimeSpan.TicksPerMillisecond;
            _echoBuffer.Enqueue(new EchoFrame
            {
                DueTicks = due,
                X = st.X,
                Y = st.Y,
                Z = st.Z,
                State = st.State,
                Flags = st.Flags,
                AnimHash = st.AnimHash,
                SpeedX = st.SpeedX,
                SpeedY = st.SpeedY,
            });
            long now = DateTime.UtcNow.Ticks;
            EchoFrame latest = default(EchoFrame);
            bool anyDue = false;
            while (_echoBuffer.Count > 0 && _echoBuffer.Peek().DueTicks <= now)
            {
                latest = _echoBuffer.Dequeue();
                anyDue = true;
            }
            while (_echoBuffer.Count > 50)
            {
                _echoBuffer.Dequeue();
            }
            if (!anyDue)
            {
                return false;
            }
            _posX = latest.X + EchoOffsetX;
            _posY = latest.Y;
            _posZ = latest.Z;
            visualState = latest.State;
            speedX = latest.SpeedX;
            speedY = latest.SpeedY;
            _faceLeft = (latest.Flags & 1) != 0;
            grounded = (latest.Flags & 2) != 0;
            animHash = latest.AnimHash;
            return true;
        }

        private string CurrentAnimStateNameLocked()
        {
            if (_lockedAnimIndex >= 0 && _lockedAnimIndex < AnimSequence.Length)
            {
                return AnimSequence[_lockedAnimIndex].ToString() + " (travado)";
            }
            if (_animCycleEnabled)
            {
                return AnimSequence[_animIndex].ToString() + " (" + (_animIndex + 1) + "/" + AnimSequence.Length + ")";
            }
            return "Idle (parado)";
        }

        private void GetMirrorVelocity(ActionVisualState dummyState, out float speedX, out float speedY, out bool grounded)
        {
            speedX = 0f;
            speedY = 0f;
            grounded = true;
            switch (dummyState)
            {
                case ActionVisualState.Running:
                    speedX = 2.5f * _patrolDir;
                    break;
                case ActionVisualState.Jump:
                    speedX = 1.5f * _patrolDir;
                    speedY = 3.0f;
                    grounded = false;
                    break;
                case ActionVisualState.DoubleJump:
                    speedX = 1.5f * _patrolDir;
                    speedY = 2.0f;
                    grounded = false;
                    break;
                case ActionVisualState.Falling:
                    speedX = 1.0f * _patrolDir;
                    speedY = -3.0f;
                    grounded = false;
                    break;
                case ActionVisualState.WallSlide:
                    speedY = -1.0f;
                    grounded = false;
                    break;
                case ActionVisualState.WallJump:
                    speedX = -2.0f * _patrolDir;
                    speedY = 2.5f;
                    grounded = false;
                    break;
                case ActionVisualState.Glide:
                    speedX = 1.2f * _patrolDir;
                    speedY = -0.5f;
                    grounded = false;
                    break;
                case ActionVisualState.Stomp:
                    speedY = -4.0f;
                    grounded = false;
                    break;
                case ActionVisualState.Dash:
                    speedX = 6.0f * _patrolDir;
                    grounded = false;
                    break;
                default:
                    break;
            }
        }
    }
}
