using System.Collections.Generic;

namespace OriCoopDedicatedServer.Net.Game.Commands
{
    /// <summary>
    /// Contrato de comando de console do novo core (cutover 02-04): mesma
    /// forma do `ConsoleCommand` do Core antigo (nome, aliases, descricao e
    /// `Execute`), mas sem nenhuma dependencia do assembly legado — o Core
    /// antigo saiu do build e permanece no disco apenas como referencia.
    /// </summary>
    public interface ConsoleCommand
    {
        string Command { get; }

        string[] Aliases { get; }

        string Description { get; }

        bool Execute(List<string> arguments, out string response);
    }
}
