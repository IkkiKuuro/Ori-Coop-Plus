using System;
using System.Collections.Generic;
using OriCoop;
using OriCoopDedicatedServer.Core;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Game
{
    public static class ServerConfig
    {
        public static Dictionary<int, Color> _ClientColorsDB = new Dictionary<int, Color>();
        public static CoopConfig Config = new CoopConfig();

        private static bool _ClientColors = true;
        private static bool _EntitySync = false;

        public static bool EntitySync
        {
            get => _EntitySync;
            set
            {
                _EntitySync = value;
                ServerSend.SetNetworkVar("ES", value.ToString());
            }
        }

        public static bool ClientColors
        {
            get => _ClientColors;
            set
            {
                _ClientColors = value;
                ServerSend.SetNetworkVar("cc", value.ToString());
            }
        }

        public static bool AllowTeleport
        {
            get => Config.AllowTeleport;
            set
            {
                Config.AllowTeleport = value;
                ServerSend.SetNetworkVar("Coop_TP", value.ToString());
                BroadcastConfig();
            }
        }

        public static bool ShareAbilities
        {
            get => Config.ShareAbilities;
            set
            {
                Config.ShareAbilities = value;
                ServerSend.SetNetworkVar("Coop_Abilities", value.ToString());
                BroadcastConfig();
            }
        }

        public static bool ShareStoryOnly
        {
            get => Config.ShareStoryOnly;
            set
            {
                Config.ShareStoryOnly = value;
                ServerSend.SetNetworkVar("Coop_StoryOnly", value.ToString());
                BroadcastConfig();
            }
        }

        public static bool ShareWorldEvents
        {
            get => Config.ShareWorldEvents;
            set
            {
                Config.ShareWorldEvents = value;
                ServerSend.SetNetworkVar("Coop_World", value.ToString());
                BroadcastConfig();
            }
        }

        public static bool ShareDoorsAndLevers
        {
            get => Config.ShareDoorsAndLevers;
            set
            {
                Config.ShareDoorsAndLevers = value;
                ServerSend.SetNetworkVar("Coop_Doors", value.ToString());
                BroadcastConfig();
            }
        }

        public static bool ShowNicknames
        {
            get => Config.ShowNicknames;
            set
            {
                Config.ShowNicknames = value;
                ServerSend.SetNetworkVar("Coop_Names", value.ToString());
                BroadcastConfig();
            }
        }

        public static Color GetClientColor(int id)
        {
            if (_ClientColorsDB.TryGetValue(id, out var value))
            {
                return value;
            }
            Color color = Color.RandomNew();
            _ClientColorsDB.Add(id, color);
            return color;
        }

        public static void SetClientColor(int id, Color color)
        {
            if (_ClientColorsDB.ContainsKey(id))
            {
                _ClientColorsDB[id] = color;
            }
            else
            {
                _ClientColorsDB.Add(id, color);
            }
        }

        public static void BroadcastConfig()
        {
            Packet packet = new Packet();
            packet.Write((int)PacketType.CONFIG_SYNC);
            packet.Write(Config.AllowTeleport);
            packet.Write(Config.ShareAbilities);
            packet.Write(Config.ShareStoryOnly);
            packet.Write(Config.ShareWorldEvents);
            packet.Write(Config.ShareDoorsAndLevers);
            packet.Write(Config.ShowNicknames);
            ServerSend.SendToAll(packet);
        }

        public static void SendConfigToClient(Client client)
        {
            Packet packet = new Packet();
            packet.Write((int)PacketType.CONFIG_SYNC);
            packet.Write(Config.AllowTeleport);
            packet.Write(Config.ShareAbilities);
            packet.Write(Config.ShareStoryOnly);
            packet.Write(Config.ShareWorldEvents);
            packet.Write(Config.ShareDoorsAndLevers);
            packet.Write(Config.ShowNicknames);
            client.Send(packet);
        }
    }
}

