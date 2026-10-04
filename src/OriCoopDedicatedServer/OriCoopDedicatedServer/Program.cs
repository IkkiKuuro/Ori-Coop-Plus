using System;
using OriCoopDedicatedServer.Core;
using OriCoopDedicatedServer.Core.CommandSystem;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer;

internal static class Program
{
	private static void Main(string[] args)
	{
		int maxplayers = 4;
		int port = 7777;
		bool autoStart = false;
		int positionalArgument = 0;

		if (args != null && args.Length > 0)
		{
			autoStart = true;
			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i].ToLower();
				if (arg == "--auto" || arg == "-auto" || arg == "/auto")
				{
					continue;
				}
				else if ((arg == "--max-players" || arg == "--maxplayers") && i + 1 < args.Length
					&& int.TryParse(args[++i], out var namedMaxPlayers))
				{
					maxplayers = ClampMaxPlayers(namedMaxPlayers);
				}
				else if (arg == "--port" && i + 1 < args.Length
					&& int.TryParse(args[++i], out var namedPort))
				{
					port = NormalizePort(namedPort);
				}
				else if (int.TryParse(arg, out var positionalValue))
				{
					if (positionalArgument++ == 0)
					{
						maxplayers = ClampMaxPlayers(positionalValue);
					}
					else
					{
						port = NormalizePort(positionalValue);
					}
				}
			}
		}

		if (!autoStart)
		{
			Logger.Info("SERVER", "ENTER MAX PLAYERS [DEFAULT 4 MAX 10] (Pressione ENTER para padrÃ£o 4)");
			string line1 = Console.ReadLine();
			if (!string.IsNullOrEmpty(line1) && int.TryParse(line1, out var result))
			{
				maxplayers = ((result > 10) ? 10 : ((result <= 0) ? 1 : result));
			}

			Logger.Info("SERVER", "ENTER SERVER PORT [DEFAULT 7777 MAX 65535] (Pressione ENTER para padrÃ£o 7777)");
			string line2 = Console.ReadLine();
			if (!string.IsNullOrEmpty(line2) && int.TryParse(line2, out var result2))
			{
				port = ((result2 > 9999) ? 9999 : ((result2 <= 0) ? 7777 : result2));
			}
		}

		Server.Start(maxplayers, port);
		Logger.Info("SERVER", "Use one of the IPv4 addresses above in the client's Network.Host setting.");
		CommandProcessor.RegisterCommands();
		new Game.OriCoopServerModule().OnEnable();
		while (!Server.StopTheServer)
		{
			string cmds = Console.ReadLine();
			if (!string.IsNullOrEmpty(cmds))
			{
				CommandProcessor.ProcessCommand(cmds);
			}
			else
			{
				System.Threading.Thread.Sleep(100);
			}
		}
	}

	private static int ClampMaxPlayers(int value)
	{
		return ((value > 10) ? 10 : ((value <= 0) ? 1 : value));
	}

	private static int NormalizePort(int value)
	{
		return ((value > 65535) ? 65535 : ((value <= 0) ? 7777 : value));
	}
}
