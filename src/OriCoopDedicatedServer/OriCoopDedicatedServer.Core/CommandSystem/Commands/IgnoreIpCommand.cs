using System.Collections.Generic;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Core.CommandSystem.Commands;

public class IgnoreIpCommand : ConsoleCommand
{
	public string Command => "ignore_ip";

	public string[] Aliases => new string[1] { "ignoreip" };

	public string Description => "Ignore ip check (DISABLE THIS NOT RECOMENDED!)";

	public bool Execute(List<string> arguments, out string response)
	{
		Server.IgnoreIpCheck = !Server.IgnoreIpCheck;
		response = $"Set value to: {Server.IgnoreIpCheck}";
		return true;
	}
}

