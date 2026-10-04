using System;
using System.Collections.Generic;
using System.Linq;
using OriCoopDedicatedServer.Core.CommandSystem.Commands;

namespace OriCoopDedicatedServer.Core.CommandSystem;

public static class CommandProcessor
{
	private static readonly Dictionary<string, ConsoleCommand> Commands = new Dictionary<string, ConsoleCommand>(StringComparer.OrdinalIgnoreCase);

	private static readonly Dictionary<string, string> CommandAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public static readonly char[] SpaceArray = new char[1] { ' ' };

	public static IEnumerable<ConsoleCommand> AllCommands => Commands.Values;

	public static void RegisterCommands()
	{
		RegisterCommand(new HelpCommand());
		RegisterCommand(new StopCommand());
		RegisterCommand(new IgnoreIpCommand());
	}

	public static void ProcessCommand(string cmds)
	{
		if (string.IsNullOrEmpty(cmds))
		{
			return;
		}
		string[] array = cmds.Trim().Split(SpaceArray, 512, StringSplitOptions.RemoveEmptyEntries);
		List<string> list = array.ToList();
		list.RemoveAt(0);
		if (TryGetCommand(array[0], out var command))
		{
			try
			{
				string response;
				bool flag = command.Execute(list, out response);
				if (!string.IsNullOrEmpty(response))
				{
					if (flag)
					{
						Logger.Info(array[0].ToUpperInvariant(), response);
					}
					else
					{
						Logger.Error(array[0].ToUpperInvariant(), response);
					}
				}
				return;
			}
			catch (Exception ex)
			{
				Logger.Error(array[0], "Command execution failed! " + ex.Message);
				return;
			}
		}
		Logger.Error("CommandProcessor", "Command " + array[0].ToUpper() + " not found!");
	}

	public static bool TryGetCommand(string query, out ConsoleCommand command)
	{
		if (CommandAliases.TryGetValue(query, out var value))
		{
			query = value;
		}
		return Commands.TryGetValue(query, out command);
	}

	public static void RegisterCommand(ConsoleCommand command)
	{
		if (string.IsNullOrWhiteSpace(command.Command))
		{
			throw new ArgumentException("Command text of " + command.GetType().Name + " cannot be null or whitespace!");
		}
		Commands.Add(command.Command, command);
		if (command.Aliases == null)
		{
			return;
		}
		string[] aliases = command.Aliases;
		foreach (string text in aliases)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new ArgumentException("Command alias of " + command.GetType().Name + " cannot be null or whitespace!");
			}
			CommandAliases.Add(text, command.Command);
		}
	}
}

