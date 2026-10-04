using System.Collections.Generic;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Core.CommandSystem.Commands;

public class StopCommand : ConsoleCommand
{
	public string Command => "stop";

	public string[] Aliases => new string[2] { "end", "exit" };

	public string Description => "stop the server..";

	public bool Execute(List<string> arguments, out string response)
	{
		response = "stooping server...";
		Server.StopTheServer = true;
		return true;
	}
}

