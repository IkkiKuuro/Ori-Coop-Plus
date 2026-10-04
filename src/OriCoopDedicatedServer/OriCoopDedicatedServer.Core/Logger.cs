using System;
using System.IO;

namespace OriCoopDedicatedServer.Core;

public static class Logger
{
	private static string LogsDir = "";

	public static string LogName = "";

	public static string GetLogsDir
	{
		get
		{
			if (string.IsNullOrEmpty(LogsDir))
			{
				string path = (LogsDir = Path.Combine(AppContext.BaseDirectory, "Logs"));
				if (Directory.Exists(path))
				{
					return LogsDir;
				}
				Directory.CreateDirectory(path);
				return LogsDir;
			}
			return LogsDir;
		}
	}

	public static string Time => $"[{DateTime.Now:HH:mm:ss}] ";

	public static void WriteLog(string log)
	{
		string text = DateTime.Now.Hour.ToString();
		string text2 = DateTime.Now.Minute.ToString();
		string text3 = DateTime.Now.Second.ToString();
		if (text.Length == 1)
		{
			text = "0" + text;
		}
		if (text2.Length == 1)
		{
			text2 = "0" + text2;
		}
		if (text3.Length == 1)
		{
			text3 = "0" + text3;
		}
		if (string.IsNullOrEmpty(LogName))
		{
			LogName = $"{GetLogsDir}/{DateTime.Now.Day}-{DateTime.Now.Month}-{DateTime.Now.Year}-_{text}_{text2}_{text3}.log";
			File.WriteAllText(LogName, "LOG STARTED!");
		}
		string text4 = "[" + text + ":" + text2 + ":" + text3 + "]";
		string text5 = text4 + " " + log;
		try
		{
			File.AppendAllText(LogName, Environment.NewLine + text5);
		}
		catch (Exception arg)
		{
			SendToConsole($"Failed to write log: {arg}", ConsoleColor.Red);
		}
	}

	public static void Info(string tag, string Msg)
	{
		string log = "[INFO] [" + tag + "] " + Msg;
		WriteLog(log);
		SendToConsole(log, ConsoleColor.Green);
	}

	public static void Warning(string tag, string Msg)
	{
		string log = "[WARNING] [" + tag + "] " + Msg;
		WriteLog(log);
		SendToConsole(log, ConsoleColor.Yellow);
	}

	public static void Error(string tag, string Msg)
	{
		string log = "[ERROR] [" + tag + "] " + Msg;
		WriteLog(log);
		SendToConsole(log, ConsoleColor.Red);
	}

	private static void SendToConsole(string log, ConsoleColor color)
	{
		Console.ForegroundColor = color;
		Console.WriteLine(Time + log);
		Console.ResetColor();
	}

	public static void Info<T>(this T cl, string msg)
	{
		Type typeFromHandle = typeof(T);
		Info(typeFromHandle.Name, msg);
	}

	public static void Warn<T>(this T cl, string msg)
	{
		Type typeFromHandle = typeof(T);
		typeFromHandle.Name.Warn(msg);
	}

	public static void Error<T>(this T cl, string msg)
	{
		Type typeFromHandle = typeof(T);
		Error(typeFromHandle.Name, msg);
	}

	public static void Debug<T>(this T cl, string msg)
	{
		Type typeFromHandle = typeof(T);
		typeFromHandle.Name.Debug(msg);
	}

	public static void Info<T>(this T cl, string Tag, string msg)
	{
		Type typeFromHandle = typeof(T);
		Info(Tag, msg);
	}

	public static void Warn<T>(this T cl, string Tag, string msg)
	{
		Type typeFromHandle = typeof(T);
		Tag.Warn(msg);
	}

	public static void Error<T>(this T cl, string Tag, string msg)
	{
		Type typeFromHandle = typeof(T);
		Error(Tag, msg);
	}

	public static void Debug<T>(this T cl, string Tag, string msg)
	{
		Type typeFromHandle = typeof(T);
		Tag.Debug(msg);
	}
}
