using System;
using System.Collections.Generic;
using System.Threading;
using OriCoop;
using OriCoopDedicatedServer.Core;
using OriCoopDedicatedServer.Core.API;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Game
{
    public static class DummyManager
    {
        public const int DummyId = 999;
        public static string DummyNick = "Bot_Amigo";
        public static bool IsActive = false;
        public static Vector3 DummyPosition = new Vector3(0, 0, 0);
        public static Color DummyColor = new Color(50, 220, 255); // Cyan

        // Modo espelho: performa a sequência de estados para validar as
        // anims do puppet no cliente (hash 0 força o resolve por estado).
        public static bool AnimCycleEnabled = false;
        private static int _animIndex = 0;
        private static int _animTickCount = 0;
        private static int _lockedAnimIndex = -1;
        private static Vector3 _animAnchor = new Vector3(0, 0, 0);
        private static float _patrolDir = 1f;
        private static bool _faceLeft = false;
        private const int AnimStateDurationTicks = 25; // 2.5 s por estado (tick 100 ms)

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

        // Modo eco (padrao): espelha as anims do jogador com ping
        // variável 20-150 ms para teste de sincronização.
        public static bool EchoEnabled = true;
        private static readonly Random _jitterRand = new Random();
        private const int EchoMinDelayMs = 20;
        private const int EchoMaxDelayMs = 150;
        private const float EchoOffsetX = 1.5f;

        private struct EchoFrame
        {
            public long DueTicks;
            public Vector3 Pos;
            public byte State;
            public byte Flags;
            public int AnimHash;
            public float SpeedX;
            public float SpeedY;
        }

        private static readonly Queue<EchoFrame> _echoBuffer = new Queue<EchoFrame>();

        public static string CurrentModeName()
        {
            if (AnimCycleEnabled)
            {
                return "ciclo-anim (" + CurrentAnimStateName() + ")";
            }
            if (EchoEnabled)
            {
                return "eco (espelho com ping 20-150 ms)";
            }
            return "parado (Idle)";
        }

        public static void SetEchoMode(bool enabled)
        {
            if (!IsActive)
            {
                Spawn();
            }
            EchoEnabled = enabled;
            if (enabled)
            {
                AnimCycleEnabled = false;
                _lockedAnimIndex = -1;
            }
            lock (_echoBuffer)
            {
                _echoBuffer.Clear();
            }
            Logger.Info("DUMMY", $"Modo eco: {(enabled ? "ATIVADO (espelha jogador, ping 20-150 ms)" : "DESATIVADO")}");
            ServerSend.SendChatMessage($"<color=yellow>[Dummy Bot]:</color> Modo eco {(enabled ? "<b>ativado</b>!" : "desativado.")}");
        }

        private static Timer _loopTimer;
        private static float _timeCounter = 0f;

        public static void Toggle()
        {
            if (IsActive)
                Despawn();
            else
                Spawn();
        }

        public static void Spawn()
        {
            if (IsActive) return;
            IsActive = true;

            // Pick a starting position near the first connected player if available
            Vector3 nearPos = GetPrimaryPlayerPosition();
            DummyPosition = new Vector3(nearPos.X + 2.5f, nearPos.Y, nearPos.Z);

            ServerConfig.SetClientColor(DummyId, DummyColor);

            _loopTimer = new Timer(Tick, null, 100, 100);
            _animAnchor = new Vector3(DummyPosition.X, DummyPosition.Y, DummyPosition.Z);
            EchoEnabled = true;
            AnimCycleEnabled = false;
            _lockedAnimIndex = -1;
            Logger.Info("DUMMY", $"[+] Dummy bot '{DummyNick}' (ID: {DummyId}) SPAWNED!");
            ServerSend.SendChatMessage($"<color=cyan>[+] Test Dummy '{DummyNick}' entrou na partida!</color>");
        }

        public static void Despawn()
        {
            if (!IsActive) return;
            IsActive = false;
            AnimCycleEnabled = false;
            _lockedAnimIndex = -1;

            _loopTimer?.Dispose();
            _loopTimer = null;

            // Send disconnect packet
            Packet dcPacket = new Packet();
            dcPacket.Write((int)PacketType.DISCONNECT);
            dcPacket.Write(DummyId);
            ServerSend.SendToAll(dcPacket);

            Logger.Info("DUMMY", $"[-] Dummy bot '{DummyNick}' (ID: {DummyId}) DESPAWNED!");
            ServerSend.SendChatMessage($"<color=red>[-] Test Dummy '{DummyNick}' saiu da partida!</color>");
        }

        private static void Tick(object state)
        {
            if (!IsActive) return;

            try
            {
                _timeCounter += 0.1f;

                ActionVisualState dummyState = ActionVisualState.Idle;
                float speedX = 0f;
                float speedY = 0f;
                bool grounded = true;
                int animHashToSend = 0;

                if (AnimCycleEnabled)
                {
                    if (_lockedAnimIndex < 0)
                    {
                        _animTickCount++;
                        if (_animTickCount >= AnimStateDurationTicks)
                        {
                            _animTickCount = 0;
                            _animIndex = (_animIndex + 1) % AnimSequence.Length;
                            Logger.Info("DUMMY", $"[DUMMY-ANIM] estado={AnimSequence[_animIndex]} ({_animIndex + 1}/{AnimSequence.Length})");
                        }
                    }
                    dummyState = AnimSequence[_animIndex];
                    GetMirrorVelocity(dummyState, out speedX, out speedY, out grounded);

                    // Patrulha em torno da âncora para o movimento combinar com a anim.
                    float newX = DummyPosition.X + speedX * 0.1f;
                    if (newX > _animAnchor.X + 4f || newX < _animAnchor.X - 4f)
                    {
                        _patrolDir = -_patrolDir;
                        newX = DummyPosition.X + speedX * 0.1f * -1f;
                    }
                    if (speedX > 0.1f) _faceLeft = false;
                    else if (speedX < -0.1f) _faceLeft = true;
                    float newY = _animAnchor.Y + (float)Math.Sin(_timeCounter * 3.0f) * 0.3f;
                    DummyPosition = new Vector3(newX, newY, _animAnchor.Z);
                }
                else if (EchoEnabled && TryDequeueEcho(out dummyState, out speedX, out speedY, out grounded, out animHashToSend))
                {
                    // Eco: pose/facing/vel do jogador com atraso 20-150 ms;
                    // DummyPosition já foi atualizada (jogador + offset lateral).
                }
                else
                {
                    Vector3 playerPos = GetPrimaryPlayerPosition();

                    // Gentle floating/hover motion relative to player position
                    float offsetX = 2.5f + (float)Math.Sin(_timeCounter * 1.5f) * 1.0f;
                    float offsetY = (float)Math.Sin(_timeCounter * 3.0f) * 0.3f;
                    DummyPosition = new Vector3(playerPos.X + offsetX, playerPos.Y + offsetY, playerPos.Z);
                    _faceLeft = offsetX < 0;
                }

                // Broadcast dummy como pacote 18 (mesmo layout do PLAYER_STATE;
                // POSITION fragmentado foi removido no rework de anims).
                // animHash 0 força o resolve por estado no cliente.
                Packet packet = new Packet();
                packet.Write(18);
                packet.Write(DummyId);
                packet.Write(DummyPosition);
                packet.Write((byte)dummyState);
                byte dummyFlags = 0;
                if (_faceLeft) dummyFlags |= 1;
                if (grounded) dummyFlags |= 2;
                packet.Write(dummyFlags);
                packet.Write(animHashToSend);
                packet.Write(speedX);
                packet.Write(speedY);
                packet.Write(DummyNick);

                ServerSend.SendToAll(packet);
            }
            catch (Exception ex)
            {
                Logger.Error("DUMMY", "Tick error: " + ex.Message);
            }
        }

        private static bool TryDequeueEcho(out ActionVisualState dummyState, out float speedX, out float speedY, out bool grounded, out int animHash)
        {
            dummyState = ActionVisualState.Idle;
            speedX = 0f;
            speedY = 0f;
            grounded = true;
            animHash = 0;

            int playerId = -1;
            DateTime freshest = DateTime.MinValue;
            foreach (var kvp in NetworkHandler.LastKnownStates)
            {
                if (kvp.Key == DummyId)
                {
                    continue;
                }
                if (kvp.Value.ReceivedAt > freshest)
                {
                    freshest = kvp.Value.ReceivedAt;
                    playerId = kvp.Key;
                }
            }
            if (playerId < 0 || (DateTime.UtcNow - freshest).TotalSeconds > 2.0)
            {
                return false;
            }

            var st = NetworkHandler.LastKnownStates[playerId];
            lock (_echoBuffer)
            {
                long due = DateTime.UtcNow.Ticks + _jitterRand.Next(EchoMinDelayMs, EchoMaxDelayMs + 1) * TimeSpan.TicksPerMillisecond;
                _echoBuffer.Enqueue(new EchoFrame
                {
                    DueTicks = due,
                    Pos = st.Pos,
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

                DummyPosition = new Vector3(latest.Pos.X + EchoOffsetX, latest.Pos.Y, latest.Pos.Z);
                dummyState = (ActionVisualState)latest.State;
                speedX = latest.SpeedX;
                speedY = latest.SpeedY;
                _faceLeft = (latest.Flags & 1) != 0;
                grounded = (latest.Flags & 2) != 0;
                animHash = latest.AnimHash;
                return true;
            }
        }

        private static void GetMirrorVelocity(ActionVisualState dummyState, out float speedX, out float speedY, out bool grounded)
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

        public static string CurrentAnimStateName()
        {
            if (_lockedAnimIndex >= 0 && _lockedAnimIndex < AnimSequence.Length)
            {
                return AnimSequence[_lockedAnimIndex].ToString() + " (travado)";
            }
            if (AnimCycleEnabled)
            {
                return AnimSequence[_animIndex].ToString() + $" ({_animIndex + 1}/{AnimSequence.Length})";
            }
            return "Idle (parado)";
        }

        public static bool SetAnimMode(bool enabled)
        {
            if (!IsActive)
            {
                Spawn();
            }
            AnimCycleEnabled = enabled;
            _lockedAnimIndex = -1;
            _animIndex = 0;
            _animTickCount = 0;
            if (enabled)
            {
                EchoEnabled = false;
            }
            Logger.Info("DUMMY", $"Modo espelho de anims: {(enabled ? "ATIVADO (ciclo 12 estados)" : "DESATIVADO")}");
            ServerSend.SendChatMessage($"<color=yellow>[Dummy Bot]:</color> Modo espelho {(enabled ? "<b>ativado</b> — ciclo de anims!" : "desativado.")}");
            return true;
        }

        public static bool LockAnimState(string name)
        {
            if (!IsActive)
            {
                Spawn();
            }
            for (int i = 0; i < AnimSequence.Length; i++)
            {
                if (string.Equals(AnimSequence[i].ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    AnimCycleEnabled = true;
                    EchoEnabled = false;
                    _lockedAnimIndex = i;
                    _animIndex = i;
                    _animTickCount = 0;
                    Logger.Info("DUMMY", $"Modo espelho travado em: {AnimSequence[i]}");
                    ServerSend.SendChatMessage($"<color=yellow>[Dummy Bot]:</color> Anim travada em <b>{AnimSequence[i]}</b>!");
                    return true;
                }
            }
            return false;
        }

        public static Vector3 GetPrimaryPlayerPosition()
        {
            foreach (var kvp in NetworkHandler.LastKnownPlayerPositions)
            {
                if (kvp.Key != DummyId)
                {
                    return kvp.Value;
                }
            }
            return new Vector3(0, 0, 0);
        }

        public static void TriggerAbility(int abilityId, string abilityName)
        {
            if (!IsActive)
            {
                Spawn();
            }

            Packet packet = new Packet();
            packet.Write((int)PacketType.SYNC_ABILITY);
            packet.Write(DummyId);
            packet.Write(abilityId);
            ServerSend.SendToAll(packet);

            Logger.Info("DUMMY", $"[DUMMY] Enviado desbloqueio de habilidade: {abilityName} ({abilityId})!");
            ServerSend.SendChatMessage($"<color=yellow>[Dummy Bot]:</color> Habilidade simulada: <b>{abilityName}</b>!");
        }

        public static void TriggerLever(int direction)
        {
            if (!IsActive)
            {
                Spawn();
            }

            // Fake MoonGuid for testing
            Packet packet = new Packet();
            packet.Write((int)PacketType.SYNC_LEVER);
            packet.Write(DummyId);
            packet.Write(0); // A
            packet.Write(0); // B
            packet.Write(0); // C
            packet.Write(1); // D
            packet.Write(direction);
            ServerSend.SendToAll(packet);

            Logger.Info("DUMMY", $"[DUMMY] Enviado teste de alavanca (direcao: {direction})!");
            ServerSend.SendChatMessage($"<color=yellow>[Dummy Bot]:</color> Acionou alavanca (dir: {direction})!");
        }

        public static void TriggerDoor()
        {
            if (!IsActive)
            {
                Spawn();
            }

            Packet packet = new Packet();
            packet.Write((int)PacketType.SYNC_DOOR);
            packet.Write(DummyId);
            packet.Write(0);
            packet.Write(0);
            packet.Write(0);
            packet.Write(1);
            ServerSend.SendToAll(packet);

            Logger.Info("DUMMY", "[DUMMY] Enviado teste de porta keystone!");
            ServerSend.SendChatMessage("<color=yellow>[Dummy Bot]:</color> Abriu porta de teste!");
        }
    }
}

