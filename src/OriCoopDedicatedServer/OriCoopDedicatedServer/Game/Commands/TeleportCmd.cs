using System;
using System.Collections.Generic;
using OriCoop;
using OriCoopDedicatedServer.Core;
using OriCoopDedicatedServer.Core.API;
using OriCoopDedicatedServer.Core.CommandSystem;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Game.Commands
{
    public class TeleportCmd : ConsoleCommand
    {
        public string Command => "tp";
        public string[] Aliases => new[] { "teleport" };
        public string Description => "Teleporta um jogador atÃ© outro (/tp <origem> <destino>)";

        public bool Execute(List<string> arguments, out string response)
        {
            if (!ServerConfig.AllowTeleport)
            {
                response = "Teleporte desativado. Use /coop tp on.";
                return false;
            }

            if (arguments.Count != 2)
            {
                response = "Uso: /tp <jogador-origem> <jogador-destino>";
                return false;
            }

            Client source = FindClient(arguments[0]);
            Client destination = FindClient(arguments[1]);
            if (source == null || destination == null)
            {
                response = "Jogador nÃ£o encontrado. Use o nick exato ou o ID.";
                return false;
            }

            if (source.Id == destination.Id)
            {
                response = "A origem e o destino precisam ser jogadores diferentes.";
                return false;
            }

            if (!NetworkHandler.LastKnownPlayerPositions.TryGetValue(destination.Id, out Vector3 position))
            {
                response = $"Ainda nÃ£o existe uma posiÃ§Ã£o recebida para {destination.Nick}.";
                return false;
            }

            Packet packet = new Packet((int)PacketType.TELEPORT_REQUEST);
            packet.Write(position);
            packet.Write(destination.Nick ?? ("Jogador " + destination.Id));
            source.udp.SendData(packet);
            ServerSend.SendChatMessage("<color=cyan>" + source.Nick + "</color> foi teleportado ate <color=cyan>" + destination.Nick + "</color>.");

            response = $"Teleportando {source.Nick} atÃ© {destination.Nick}.";
            return true;
        }

        private static Client FindClient(string query)
        {
            foreach (Client client in Server.Clients.Values)
            {
                if (client == null || !client.IsReady)
                {
                    continue;
                }

                if (string.Equals(client.Nick, query, StringComparison.OrdinalIgnoreCase) ||
                    client.Id.ToString() == query)
                {
                    return client;
                }
            }

            return null;
        }
    }
}
