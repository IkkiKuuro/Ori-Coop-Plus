using System;
using System.Collections.Generic;
using OriCoop;
using OriCoopDedicatedServer.Core;
using OriCoopDedicatedServer.Core.API;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Game
{
    public static class NetworkHandler
    {
        public static Dictionary<int, Vector3> LastKnownPlayerPositions = new Dictionary<int, Vector3>();
        public static HashSet<int> UnlockedAbilities = new HashSet<int>();

        public static void OnPlayerJoin(Client pl)
        {
            if (!ServerConfig.ClientColors)
            {
                Packet packet = new Packet((int)PacketType.COLOR);
                ServerConfig.GetClientColor(pl.Id).WritePacket(ref packet);
                pl.Send(packet);
            }
            else
            {
                Logger.Info(pl.Id.ToString(), "CLIENT JOINED! WAITING CLIENT COLOR...");
            }

            // Sync server settings to client
            ServerConfig.SendConfigToClient(pl);

            // Sync already unlocked abilities to new player
            if (ServerConfig.ShareAbilities && UnlockedAbilities.Count > 0)
            {
                foreach (int abilityId in UnlockedAbilities)
                {
                    Packet abPacket = new Packet();
                    abPacket.Write((int)PacketType.SYNC_ABILITY);
                    abPacket.Write(-1); // from server history
                    abPacket.Write(abilityId);
                    pl.Send(abPacket);
                }
                Logger.Info(pl.Id.ToString(), $"Sincronizadas {UnlockedAbilities.Count} habilidades jÃ¡ desbloqueadas para o novo jogador.");
            }
        }

        public static void OnPacketRecived(int packetId, Client pl, Packet packet)
        {
            switch ((PacketType)packetId)
            {
                case PacketType.COLOR:
                {
                    if (ServerConfig.ClientColors)
                    {
                        Color color = Color.FromPacket(ref packet);
                        Logger.Info($"[{pl.Id}]", $"RECIVED CUSTOM COLOR: {color}");
                        ServerConfig.SetClientColor(pl.Id, color);
                        Packet packet5 = new Packet((int)PacketType.COLOR);
                        packet5.Write(pl.Id);
                        color.WritePacket(ref packet5);
                        pl.Send(packet5);
                    }
                    break;
                }
                case PacketType.POSITION:
                {
                    Vector3 pos = packet.ReadVector3();
                    bool faceLeft = packet.ReadBool();

                    LastKnownPlayerPositions[pl.Id] = pos;

                    Packet packet4 = new Packet();
                    packet4.Write((int)PacketType.POSITION);
                    packet4.Write(pl.Id);
                    packet4.Write(pos);
                    ServerConfig.GetClientColor(pl.Id).WritePacket(ref packet4);
                    packet4.Write(faceLeft);
                    packet4.Write(pl.Nick ?? ("Player " + pl.Id)); // Send nickname!

                    ServerSend.SendToAll(pl.Id, packet4);
                    break;
                }
                case PacketType.ANIM:
                {
                    string anim = packet.ReadString();
                    Packet packet3 = new Packet();
                    packet3.Write((int)PacketType.ANIM);
                    packet3.Write(pl.Id);
                    packet3.Write(anim);
                    ServerSend.SendToAll(pl.Id, packet3);
                    break;
                }
                case PacketType.TELEPORT_REQUEST:
                {
                    if (!ServerConfig.AllowTeleport)
                    {
                        ServerSend.SendChatMessage("<color=yellow>Teleporte desativado pelo servidor.</color>");
                        break;
                    }

                    int targetId = packet.ReadInt();
                    Client target;
                    Vector3 targetPosition;
                    if (!Server.Clients.TryGetValue(targetId, out target) ||
                        target == null || !target.IsReady ||
                        !LastKnownPlayerPositions.TryGetValue(targetId, out targetPosition))
                    {
                        ServerSend.SendChatMessage("<color=yellow>Destino de teleporte indisponivel.</color>");
                        break;
                    }

                    Packet teleport = new Packet((int)PacketType.TELEPORT_REQUEST);
                    teleport.Write(targetPosition);
                    teleport.Write(target.Nick ?? ("Player " + target.Id));
                    pl.Send(teleport);
                    ServerSend.SendChatMessage("<color=cyan>" + pl.Nick + "</color> foi teleportado ate <color=cyan>" + target.Nick + "</color>.");
                    break;
                }
                case PacketType.SKILL:
                {
                    int skill = packet.ReadInt();
                    Packet packet2 = new Packet();
                    packet2.Write((int)PacketType.SKILL);
                    packet2.Write(pl.Id);
                    packet2.Write(skill);
                    ServerSend.SendToAll(pl.Id, packet2);
                    break;
                }
                case PacketType.SYNC_ABILITY:
                {
                    int abilityId = packet.ReadInt();
                    Logger.Info($"[{pl.Id}]", $"HABILIDADE DESBLOQUEADA: {abilityId}");
                    if (ServerConfig.ShareAbilities)
                    {
                        UnlockedAbilities.Add(abilityId);
                        Packet abPacket = new Packet();
                        abPacket.Write((int)PacketType.SYNC_ABILITY);
                        abPacket.Write(pl.Id);
                        abPacket.Write(abilityId);
                        ServerSend.SendToAll(pl.Id, abPacket);
                    }
                    break;
                }
                case PacketType.SYNC_LEVER:
                {
                    int a = packet.ReadInt();
                    int b = packet.ReadInt();
                    int c = packet.ReadInt();
                    int d = packet.ReadInt();
                    int dir = packet.ReadInt();

                    if (ServerConfig.ShareDoorsAndLevers)
                    {
                        Packet levPacket = new Packet();
                        levPacket.Write((int)PacketType.SYNC_LEVER);
                        levPacket.Write(pl.Id);
                        levPacket.Write(a);
                        levPacket.Write(b);
                        levPacket.Write(c);
                        levPacket.Write(d);
                        levPacket.Write(dir);
                        ServerSend.SendToAll(pl.Id, levPacket);
                    }
                    break;
                }
                case PacketType.SYNC_DOOR:
                {
                    int a = packet.ReadInt();
                    int b = packet.ReadInt();
                    int c = packet.ReadInt();
                    int d = packet.ReadInt();

                    if (ServerConfig.ShareDoorsAndLevers)
                    {
                        Packet doorPacket = new Packet();
                        doorPacket.Write((int)PacketType.SYNC_DOOR);
                        doorPacket.Write(pl.Id);
                        doorPacket.Write(a);
                        doorPacket.Write(b);
                        doorPacket.Write(c);
                        doorPacket.Write(d);
                        ServerSend.SendToAll(pl.Id, doorPacket);
                    }
                    break;
                }
                case PacketType.SYNC_WORLDEVENT:
                {
                    int a = packet.ReadInt();
                    int b = packet.ReadInt();
                    int c = packet.ReadInt();
                    int d = packet.ReadInt();
                    int val = packet.ReadInt();

                    if (ServerConfig.ShareWorldEvents)
                    {
                        Packet wePacket = new Packet();
                        wePacket.Write((int)PacketType.SYNC_WORLDEVENT);
                        wePacket.Write(pl.Id);
                        wePacket.Write(a);
                        wePacket.Write(b);
                        wePacket.Write(c);
                        wePacket.Write(d);
                        wePacket.Write(val);
                        ServerSend.SendToAll(pl.Id, wePacket);
                    }
                    break;
                }
                case PacketType.DUMMY_ACTION:
                {
                    int action = packet.ReadInt();
                    if (action == 0)
                    {
                        DummyManager.Toggle();
                    }
                    else if (action == 1)
                    {
                        int abId = packet.ReadInt();
                        string abName = GetAbilityName(abId);
                        DummyManager.TriggerAbility(abId, abName);
                    }
                    break;
                }
                case PacketType.DISCONNECT:
                {
                    Logger.Info("SERVER", "PLAYER: " + pl.Nick + " LEFT!");
                    if (LastKnownPlayerPositions.ContainsKey(pl.Id))
                    {
                        LastKnownPlayerPositions.Remove(pl.Id);
                    }
                    break;
                }
                default:
                    break;
            }
        }

        private static string GetAbilityName(int id)
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
