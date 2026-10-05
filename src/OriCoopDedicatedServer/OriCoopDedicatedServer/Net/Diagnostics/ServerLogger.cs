using System;
using System.IO;
using System.Text;

namespace OriCoopDedicatedServer.Net.Diagnostics
{
    public enum ServerLogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>
    /// Abstracao de log injetavel do novo core (D-13): explica cada drop.
    /// </summary>
    public interface ILogger
    {
        void Log(ServerLogLevel level, string tag, string message);
    }

    /// <summary>
    /// Log em console + arquivo com niveis e lock (D-16).
    /// </summary>
    public sealed class FileConsoleLogger : ILogger
    {
        private readonly object _sync = new object();
        private readonly string _filePath;
        private readonly ServerLogLevel _minLevel;

        public FileConsoleLogger(string filePath, ServerLogLevel minLevel)
        {
            _filePath = filePath;
            _minLevel = minLevel;
        }

        public FileConsoleLogger(string filePath)
            : this(filePath, ServerLogLevel.Info)
        {
        }

        public void Log(ServerLogLevel level, string tag, string message)
        {
            if (level < _minLevel)
            {
                return;
            }
            string safeTag = string.IsNullOrEmpty(tag) ? "NET" : tag;
            string line = "[" + DateTime.UtcNow.ToString("HH:mm:ss") + "]"
                + "[" + level.ToString().ToUpperInvariant() + "]"
                + "[" + safeTag + "] " + (message ?? string.Empty);
            lock (_sync)
            {
                Console.WriteLine(line);
                if (!string.IsNullOrEmpty(_filePath))
                {
                    try
                    {
                        File.AppendAllText(_filePath, line + Environment.NewLine, Encoding.UTF8);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }
}
