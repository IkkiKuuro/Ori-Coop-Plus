using System.Collections.Generic;

namespace OriCoopDedicatedServer.Core.CommandSystem;

public interface ConsoleCommand
{
	string Command { get; }

	string[] Aliases { get; }

	string Description { get; }

	bool Execute(List<string> arguments, out string response);
}

