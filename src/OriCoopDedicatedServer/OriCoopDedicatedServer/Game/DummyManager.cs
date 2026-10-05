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
                packet.Write(0);
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

            Logger.Info("DUMMY", $"[DUMMY] Enviado teste de alavanca (direÃ§Ã£o: {direction})!");
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

