using System.Collections.Generic;
using OriCoopDedicatedServer.Core.CommandSystem;

namespace OriCoopDedicatedServer.Game.Commands
{
    public class AllowEntitySyncCmd : ConsoleCommand
    {
        public string Command => "entitysync";
        public string[] Aliases => new string[2] { "es", "sync" };
        public string Description => "Enable or Disable entity sync";

        public bool Execute(List<string> arguments, out string response)
        {
            if (arguments.Count >= 1)
            {
                string arg = arguments[0].ToLower();
                if (arg == "on" || arg == "true" || arg == "1" || arg == "sim")
                {
                    ServerConfig.EntitySync = true;
                }
                else if (arg == "off" || arg == "false" || arg == "0" || arg == "nao")
                {
                    ServerConfig.EntitySync = false;
                }
                else
                {
                    ServerConfig.EntitySync = !ServerConfig.EntitySync;
                }
            }
            else
            {
                ServerConfig.EntitySync = !ServerConfig.EntitySync;
            }
            response = $"Sincronizacao de entidades: {(ServerConfig.EntitySync ? "ATIVADO" : "DESATIVADO")}";
            return true;
        }
    }
}

