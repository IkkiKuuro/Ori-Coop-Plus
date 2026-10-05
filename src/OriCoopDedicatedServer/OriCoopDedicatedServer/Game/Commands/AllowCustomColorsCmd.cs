using System.Collections.Generic;
using OriCoopDedicatedServer.Core.CommandSystem;

namespace OriCoopDedicatedServer.Game.Commands
{
    public class AllowCustomColorsCmd : ConsoleCommand
    {
        public string Command => "clientcolors";
        public string[] Aliases => new string[3] { "cc", "clientc", "ccolors" };
        public string Description => "Enable or Disable client colors";

        public bool Execute(List<string> arguments, out string response)
        {
            if (arguments.Count >= 1)
            {
                string arg = arguments[0].ToLower();
                if (arg == "on" || arg == "true" || arg == "1" || arg == "sim")
                {
                    ServerConfig.ClientColors = true;
                }
                else if (arg == "off" || arg == "false" || arg == "0" || arg == "nao")
                {
                    ServerConfig.ClientColors = false;
                }
                else
                {
                    ServerConfig.ClientColors = !ServerConfig.ClientColors;
                }
            }
            else
            {
                ServerConfig.ClientColors = !ServerConfig.ClientColors;
            }
            response = $"Cores de clientes: {(ServerConfig.ClientColors ? "ATIVADO" : "DESATIVADO")}";
            return true;
        }
    }
}

