using System.Collections.Generic;
using System.Text;

namespace OriCoopDedicatedServer.Core.CommandSystem.Commands;

public class HelpCommand : ConsoleCommand
{
	public readonly StringBuilder _helpBuilder = new StringBuilder();

	public string Command => "help";

	public string[] Aliases => new string[1] { "h" };

	public string Description => "show commands";

	public bool Execute(List<string> arguments, out string response)
	{
		response = GetCommandList("Command list:");
		return true;
	}

	public string GetCommandList(string header)
	{
		_helpBuilder.Clear();
		_helpBuilder.Append(header);
		foreach (ConsoleCommand allCommand in CommandProcessor.AllCommands)
		{
			_helpBuilder.AppendLine();
			_helpBuilder.Append(allCommand.Command);
			_helpBuilder.Append(" - ");
			_helpBuilder.Append(allCommand.Description);
			if (allCommand.Aliases != null && allCommand.Aliases.Length != 0)
			{
				_helpBuilder.Append(" - Aliases: ");
				_helpBuilder.Append(string.Join(", ", allCommand.Aliases));
			}
		}
		return _helpBuilder.ToString();
	}
}

