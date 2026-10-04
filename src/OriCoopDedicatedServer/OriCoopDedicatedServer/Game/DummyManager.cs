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
            Logger.Info("DUMMY", $"[+] Dummy bot '{DummyNick}' (ID: {DummyId}) SPAWNED!");
            ServerSend.SendChatMessage($"<color=cyan>[+] Test Dummy '{DummyNick}' entrou na partida!</color>");
        }

        public static void Despawn()
        {
            if (!IsActive) return;
            IsActive = false;

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
                Vector3 playerPos = GetPrimaryPlayerPosition();

                // Gentle floating/hover motion relative to player position
                float offsetX = 2.5f + (float)Math.Sin(_timeCounter * 1.5f) * 1.0f;
                float offsetY = (float)Math.Sin(_timeCounter * 3.0f) * 0.3f;
                DummyPosition = new Vector3(playerPos.X + offsetX, playerPos.Y + offsetY, playerPos.Z);

                // Broadcast dummy position to all clients
                Packet packet = new Packet();
                packet.Write((int)PacketType.POSITION);
                packet.Write(DummyId);
                packet.Write(DummyPosition);
                DummyColor.WritePacket(ref packet);
                packet.Write(offsetX < 0); // face left if left of player
                packet.Write(DummyNick);   // Nickname!

                ServerSend.SendToAll(packet);
            }
            catch (Exception ex)
            {
                Logger.Error("DUMMY", "Tick error: " + ex.Message);
            }
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

