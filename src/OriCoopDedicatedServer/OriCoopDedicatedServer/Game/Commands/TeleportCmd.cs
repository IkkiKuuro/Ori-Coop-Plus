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
        public string Description => "Teleporta um jogador ate outro (/tp <origem> <destino>)";

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
            if (source == null)
            {
                response = "Jogador de origem não encontrado. Use o nick exato ou o ID.";
                return false;
            }

            Vector3 position;
            string destNick;

            if (DummyManager.IsActive && (arguments[1] == DummyManager.DummyId.ToString() || string.Equals(arguments[1], DummyManager.DummyNick, StringComparison.OrdinalIgnoreCase)))
            {
                position = DummyManager.DummyPosition;
                destNick = DummyManager.DummyNick;
            }
            else
            {
                Client destination = FindClient(arguments[1]);
                if (destination == null)
                {
                    response = "Jogador de destino não encontrado. Use o nick exato ou o ID.";
                    return false;
                }

                if (source.Id == destination.Id)
                {
                    response = "A origem e o destino precisam ser jogadores diferentes.";
                    return false;
                }

                if (!NetworkHandler.LastKnownPlayerPositions.TryGetValue(destination.Id, out position))
                {
                    response = $"Ainda não existe uma posição recebida para {destination.Nick}.";
                    return false;
                }
                destNick = destination.Nick ?? ("Jogador " + destination.Id);
            }

            Packet packet = new Packet((int)PacketType.TELEPORT_REQUEST);
            packet.Write(position);
            packet.Write(destNick);
            source.udp.SendData(packet);
            ServerSend.SendChatMessage("<color=cyan>" + source.Nick + "</color> foi teleportado ate <color=cyan>" + destNick + "</color>.");

            response = $"Teleportando {source.Nick} até {destNick}.";
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
