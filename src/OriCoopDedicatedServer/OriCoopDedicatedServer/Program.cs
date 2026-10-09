using System;
using OriCoopDedicatedServer.Net.Diagnostics;
using OriCoopDedicatedServer.Net.Game;
using OriCoopDedicatedServer.Net.Game.Commands;

namespace OriCoopDedicatedServer;

internal static class Program
{
	// Cutover 02-04 (D-14, quebra one-way ja aprovada): o novo core e o unico
	// path — nao ha mais branch --net2 nem path do Core antigo. A flag --net2
	// ainda e aceita como no-op para nao quebrar scripts/smoke existentes.
	// O Core antigo permanece no disco como referencia, fora do build.
	private static void Main(string[] args)
	{
		try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
		int maxplayers = 4;
		int port = 7777;
		bool autoStart = false;
		bool meaningfulArg = false;
		int positionalArgument = 0;

		if (args != null && args.Length > 0)
		{
			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i].ToLower();
				if (arg == "--net2" || arg == "-net2" || arg == "/net2")
				{
					// No-op: novo core e o default desde o cutover.
					// Nao conta como avanco automatico sozinho.
					continue;
				}
				if (arg == "--auto" || arg == "-auto" || arg == "/auto")
				{
					meaningfulArg = true;
					continue;
				}
				else if ((arg == "--max-players" || arg == "--maxplayers") && i + 1 < args.Length
					&& int.TryParse(args[++i], out var namedMaxPlayers))
				{
					meaningfulArg = true;
					maxplayers = ClampMaxPlayers(namedMaxPlayers);
				}
				else if (arg == "--port" && i + 1 < args.Length
					&& int.TryParse(args[++i], out var namedPort))
				{
					meaningfulArg = true;
					port = NormalizePort(namedPort);
				}
				else if (int.TryParse(arg, out var positionalValue))
				{
					meaningfulArg = true;
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
		autoStart = meaningfulArg;

		if (!autoStart)
		{
			Console.WriteLine("ENTER MAX PLAYERS [DEFAULT 4 MAX 10] (Pressione ENTER para padrao 4)");
			string? line1 = Console.ReadLine();
			if (!string.IsNullOrEmpty(line1) && int.TryParse(line1, out var result))
			{
				maxplayers = ClampMaxPlayers(result);
			}

			Console.WriteLine("ENTER SERVER PORT [DEFAULT 7777 MAX 65535] (Pressione ENTER para padrao 7777)");
			string? line2 = Console.ReadLine();
			if (!string.IsNullOrEmpty(line2) && int.TryParse(line2, out var result2))
			{
				port = NormalizePort(result2);
			}
		}

		RunHost(maxplayers, port);
	}

	private static void RunHost(int maxplayers, int port)
	{
		var boot = new ServerBoot(port, maxplayers);
		var log = boot.Log;
		var registry = new CommandRegistry(boot);
		OriCommands.RegisterAll(registry);
		using (var cts = new System.Threading.CancellationTokenSource())
		{
			System.Console.CancelKeyPress += (sender, e) =>
			{
				e.Cancel = true;
				try { cts.Cancel(); } catch { }
			};
			System.Threading.Tasks.Task runTask = boot.StartAsync(cts.Token);
			log.Log(ServerLogLevel.Info, "SERVER", "Server started on " + port);
			log.Log(ServerLogLevel.Info, "SERVER", "Ori Coop Plus Server Module CARREGADO");
			log.Log(ServerLogLevel.Info, "SERVER", "Use one of the IPv4 addresses above in the client's Network.Host setting.");
			log.Log(ServerLogLevel.Info, "SERVER", "Digite stop para encerrar.");
			while (!cts.IsCancellationRequested)
			{
				var rawOpt = System.Console.ReadLine();
				if (rawOpt == null)
				{
					// stdin fechado/redirecionado (ex. dotnet run em script):
					// nao encerra; aguarda Ctrl+C ou morte do processo.
					log.Log(ServerLogLevel.Debug, "SERVER", "stdin indisponivel; aguardando Ctrl+C ou encerramento externo.");
					try { cts.Token.WaitHandle.WaitOne(); } catch { }
					break;
				}
				string line = rawOpt.Trim();
				if (line.Length == 0)
				{
					continue;
				}
				string lowered = line.ToLowerInvariant();
				if (lowered == "stop" || lowered == "quit" || lowered == "exit" || lowered == "sair")
				{
					string stopResponse;
					registry.ExecuteLine("stop", null, out stopResponse);
					if (!string.IsNullOrEmpty(stopResponse))
					{
						log.Log(ServerLogLevel.Info, "CMD", stopResponse);
					}
					try { boot.RequestStop(); } catch { }
					break;
				}
				string response;
				bool ok = registry.ExecuteLine(line, null, out response);
				if (!string.IsNullOrEmpty(response))
				{
					log.Log(ok ? ServerLogLevel.Info : ServerLogLevel.Warning, "CMD", response);
				}
			}
			try { boot.StopAsync().GetAwaiter().GetResult(); }
			catch (System.OperationCanceledException) { }
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
