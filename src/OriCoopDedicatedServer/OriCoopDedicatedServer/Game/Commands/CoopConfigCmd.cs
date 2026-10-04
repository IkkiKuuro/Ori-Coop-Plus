using System;
using System.Collections.Generic;
using OriCoopDedicatedServer.Core.CommandSystem;

namespace OriCoopDedicatedServer.Game.Commands
{
    public class CoopConfigCmd : ConsoleCommand
    {
        public string Command => "coop";
        public string[] Aliases => new string[] { "coopconfig", "config", "cfg" };
        public string Description => "Configure and toggle Ori Coop Plus features (/coop [tp|abilities|story|world|doors|names] [on|off])";

        public bool Execute(List<string> arguments, out string response)
        {
            if (arguments.Count == 0)
            {
                response = "\n=== Ori Coop Plus - ConfiguraÃ§Ãµes Atuais ===" +
                    $"\n [1] Teleporte (/coop tp): {(ServerConfig.AllowTeleport ? "ATIVADO" : "DESATIVADO")}" +
                    $"\n [2] Compartilhar Habilidades (/coop abilities): {(ServerConfig.ShareAbilities ? "ATIVADO" : "DESATIVADO")}" +
                    $"\n [3] Apenas Habilidades de HistÃ³ria (/coop story): {(ServerConfig.ShareStoryOnly ? "ATIVADO" : "DESATIVADO")}" +
                    $"\n [4] Eventos do Mundo (/coop world): {(ServerConfig.ShareWorldEvents ? "ATIVADO" : "DESATIVADO")}" +
                    $"\n [5] Portas e Alavancas (/coop doors): {(ServerConfig.ShareDoorsAndLevers ? "ATIVADO" : "DESATIVADO")}" +
                    $"\n [6] Nomes Flutuantes (/coop names): {(ServerConfig.ShowNicknames ? "ATIVADO" : "DESATIVADO")}" +
                    "\n============================================\nPara alterar: /coop <opcao> [on/off]";
                return true;
            }

            string opt = arguments[0].ToLower();
            bool? targetState = null;
            if (arguments.Count >= 2)
            {
                string stateStr = arguments[1].ToLower();
                if (stateStr == "on" || stateStr == "true" || stateStr == "1" || stateStr == "sim")
                    targetState = true;
                else if (stateStr == "off" || stateStr == "false" || stateStr == "0" || stateStr == "nao")
                    targetState = false;
            }

            switch (opt)
            {
                case "tp":
                case "teleport":
                    ServerConfig.AllowTeleport = targetState ?? !ServerConfig.AllowTeleport;
                    response = $"Teleporte configurado para: {(ServerConfig.AllowTeleport ? "ATIVADO" : "DESATIVADO")}";
                    return true;

                case "abilities":
                case "ability":
                case "skills":
                    ServerConfig.ShareAbilities = targetState ?? !ServerConfig.ShareAbilities;
                    response = $"Compartilhar Habilidades configurado para: {(ServerConfig.ShareAbilities ? "ATIVADO" : "DESATIVADO")}";
                    return true;

                case "story":
                case "storyonly":
                    ServerConfig.ShareStoryOnly = targetState ?? !ServerConfig.ShareStoryOnly;
                    response = $"Apenas Habilidades de HistÃ³ria configurado para: {(ServerConfig.ShareStoryOnly ? "ATIVADO" : "DESATIVADO")}";
                    return true;

                case "world":
                case "events":
                    ServerConfig.ShareWorldEvents = targetState ?? !ServerConfig.ShareWorldEvents;
                    response = $"Eventos de Mundo configurado para: {(ServerConfig.ShareWorldEvents ? "ATIVADO" : "DESATIVADO")}";
                    return true;

                case "doors":
                case "levers":
                case "door":
                case "lever":
                    ServerConfig.ShareDoorsAndLevers = targetState ?? !ServerConfig.ShareDoorsAndLevers;
                    response = $"Portas e Alavancas configurado para: {(ServerConfig.ShareDoorsAndLevers ? "ATIVADO" : "DESATIVADO")}";
                    return true;

                case "names":
                case "nick":
                case "nicks":
                case "nicknames":
                    ServerConfig.ShowNicknames = targetState ?? !ServerConfig.ShowNicknames;
                    response = $"Nomes Flutuantes configurado para: {(ServerConfig.ShowNicknames ? "ATIVADO" : "DESATIVADO")}";
                    return true;

                default:
                    response = "OpÃ§Ã£o invÃ¡lida. Use: tp, abilities, story, world, doors, names";
                    return false;
            }
        }
    }
}

