using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OriCoopDedicatedServer.Net.Diagnostics;
using OriCoopDedicatedServer.Net.Session;

namespace OriCoopDedicatedServer.Net.Game
{
    /// <summary>
    /// Contexto de servidor para os comandos por instancia (02-03, tarefa 3):
    /// sessoes, config, dummy, regras de jogo (envio/chat/busca) e parada.
    /// Implementado pelo ServerBoot; sem estatico global.
    /// </summary>
    public interface IServerContext
    {
        SessionManager Sessions { get; }

        ConfigStore Config { get; }

        DummyBot Dummy { get; }

        GameHandlers Game { get; }

        ILogger Log { get; }

        /// <summary>Sinaliza o CancellationToken do host (comando stop).</summary>
        Action RequestStop { get; }
    }

    /// <summary>
    /// Bootstrap do novo core com a camada Game (D-13/D-16): carrega
    /// serverconfig.json, instancia logger, sessoes, config, transporte
    /// (host), dummy e handlers, liga os Changed da config ao broadcast e
    /// expoe StartAsync/StopAsync com CancellationToken ligado ao console.
    /// Nao zera opcoes no boot; nao toca no Core antigo.
    /// </summary>
    public sealed class ServerBoot : IServerContext
    {
        private readonly int _port;
        private readonly int _maxPlayers;
        private readonly ILogger _log;
        private readonly SessionManager _sessions;
        private readonly ConfigStore _config;
        private readonly NetServerHost _host;
        private readonly DummyBot _dummy;
        private readonly GameHandlers _handlers;
        private CancellationTokenSource? _linkedCts;
        private Task? _runTask;
        private readonly object _sync = new object();

        public ServerBoot(int port, int maxPlayers)
            : this(port, maxPlayers, new FileConsoleLogger(DefaultLogPath()), null)
        {
        }

        /// <summary>
        /// Log em `Logs/` ao lado do diretorio de trabalho (D-16): console +
        /// arquivo com niveis. O diretorio e criado aqui para o append nunca
        /// falhar por pasta ausente.
        /// </summary>
        public static string DefaultLogPath()
        {
            try
            {
                Directory.CreateDirectory("Logs");
            }
            catch (Exception)
            {
            }
            return Path.Combine("Logs", "server.log");
        }

        public ServerBoot(int port, int maxPlayers, ILogger log, string? configPath)
        {
            _port = port;
            _maxPlayers = maxPlayers;
            _log = log ?? throw new ArgumentNullException("log");
            _sessions = new SessionManager(maxPlayers, _log);
            _config = string.IsNullOrEmpty(configPath) ? new ConfigStore(_log) : new ConfigStore(_log, configPath);
            _config.Load();
            _host = new NetServerHost(port, maxPlayers, _log, _sessions);
            _dummy = new DummyBot(_log, _host, _config);
            _handlers = new GameHandlers(_sessions, _config, _dummy, _host, _log);
            _dummy.PrimaryPlayerProvider = _handlers.GetPrimaryPlayerView;
            _host.Game = _handlers;
            _config.Changed += OnConfigChanged;
        }

        public SessionManager Sessions
        {
            get { return _sessions; }
        }

        public ConfigStore Config
        {
            get { return _config; }
        }

        public DummyBot Dummy
        {
            get { return _dummy; }
        }

        public GameHandlers Game
        {
            get { return _handlers; }
        }

        public ILogger Log
        {
            get { return _log; }
        }

        public Action RequestStop
        {
            get { return DoRequestStop; }
        }

        public Task StartAsync(CancellationToken ct)
        {
            lock (_sync)
            {
                if (_runTask != null)
                {
                    return _runTask;
                }
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _runTask = _host.RunAsync(_linkedCts.Token);
                return _runTask;
            }
        }

        public async Task StopAsync()
        {
            DoRequestStop();
            Task? run;
            lock (_sync)
            {
                run = _runTask;
            }
            if (run != null)
            {
                try
                {
                    await run.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            _dummy.Stop();
            _log.Log(ServerLogLevel.Info, "NET2", "ServerBoot encerrado.");
        }

        private void DoRequestStop()
        {
            lock (_sync)
            {
                try
                {
                    if (_linkedCts != null)
                    {
                        _linkedCts.Cancel();
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void OnConfigChanged()
        {
            // Broadcast CONFIG_SYNC 16 confiavel a cada mudanca (D-10/D-16);
            // save ja aconteceu dentro do ConfigStore. Fire-and-forget com
            // log: o comando de console nao deve travar por causa do socket.
            try
            {
                _ = _host.GameBroadcastReliableAsync(
                    (int)OriCoop.PacketType.CONFIG_SYNC,
                    _config.BuildConfigPayload(),
                    CancellationToken.None).ContinueWith(delegate (Task t)
                    {
                        if (t.IsFaulted)
                        {
                            _log.Log(ServerLogLevel.Warning, "CONFIG",
                                "Broadcast pos-mudanca falhou: " + t.Exception.GetType().Name);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "CONFIG", "Broadcast pos-mudanca falhou: " + ex.GetType().Name);
            }
        }
    }
}
