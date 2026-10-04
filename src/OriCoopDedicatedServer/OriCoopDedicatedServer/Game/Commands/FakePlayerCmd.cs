using System.Collections.Generic;
using OriCoopDedicatedServer.Core.CommandSystem;

namespace OriCoopDedicatedServer.Game.Commands
{
    public class FakePlayerCmd : ConsoleCommand
    {
        public string Command => "fakeplayer";
        public string[] Aliases => new string[2] { "fakepl", "fp" };
        public string Description => "Toggle advanced test dummy bot for multiplayer testing (/dummy)";

        public bool Execute(List<string> arguments, out string response)
        {
            DummyManager.Toggle();
            response = $"[FakePlayer / Dummy] Bot de testes agora estÃ¡: {(DummyManager.IsActive ? "ATIVO (ID 999 - 'Bot_Amigo')" : "DESATIVADO")}. Use /dummy para mais opÃ§Ãµes.";
            return true;
        }
    }
}

