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
            ServerConfig.EntitySync = !ServerConfig.EntitySync;
            response = $"SET USE ENTITY SYNC TO: {ServerConfig.EntitySync}";
            return true;
        }
    }
}

