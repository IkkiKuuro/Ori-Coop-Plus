using System;
using System.Collections.Generic;
using OriCoopDedicatedServer.Core.CommandSystem;
using OriCoopDedicatedServer.Net.Diagnostics;
using OriCoopDedicatedServer.Net.Session;

namespace OriCoopDedicatedServer.Net.Game.Commands
{
    /// <summary>
    /// Alvo de sessao opcional: comandos que podem responder no chat do
    /// solicitante (ex. help via chat) recebem a sessao antes do Execute.
    /// </summary>
    public interface ISessionTarget
    {
        Session.Session? TargetSession { get; set; }
    }

    /// <summary>
    /// Parser de comandos por instancia sobre o novo core (D-13): dicionario
    /// case-insensitive de nome para handler mais aliases, recebendo o
    /// IServerContext (sessoes, config, dummy, game, stop) no construtor.
    /// Nomes/aliases identicos aos comandos antigos (D-16): coop, tp/teleport,
    /// dummy/bot/testbot/fakeplayer/fakepl/fp, clientcolors/cc/clientc/ccolors,
    /// entitysync/es/sync, help, stop. Nao toca nos comandos do Core antigo;
    /// a fiacao no loop de console acontece no cutover (02-04).
    /// </summary>
    public sealed class CommandRegistry
    {
        private readonly Dictionary<string, ConsoleCommand> _commands =
            new Dictionary<string, ConsoleCommand>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly IServerContext _context;

        public CommandRegistry(IServerContext context)
        {
            _context = context ?? throw new ArgumentNullException("context");
        }

        public IServerContext Context
        {
            get { return _context; }
        }

        public IEnumerable<ConsoleCommand> All
        {
            get { return _commands.Values; }
        }

        public void Register(ConsoleCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }
            if (string.IsNullOrWhiteSpace(command.Command))
            {
                throw new ArgumentException("Command text of " + command.GetType().Name + " cannot be null or whitespace!");
            }
            if (_commands.ContainsKey(command.Command))
            {
                throw new ArgumentException("Command " + command.Command + " already registered!");
            }
            _commands.Add(command.Command, command);
            if (command.Aliases != null)
            {
                foreach (string alias in command.Aliases)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        throw new ArgumentException("Command alias of " + command.GetType().Name + " cannot be null or whitespace!");
                    }
                    if (_aliases.ContainsKey(alias) || _commands.ContainsKey(alias))
                    {
                        throw new ArgumentException("Alias " + alias + " already registered!");
                    }
                    _aliases.Add(alias, command.Command);
                }
            }
        }

        public bool TryGet(string query, out ConsoleCommand command)
        {
            command = null!;
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }
            string name = query.Trim();
            string? canonical;
            if (_aliases.TryGetValue(name, out canonical) && !string.IsNullOrEmpty(canonical))
            {
                name = canonical;
            }
            ConsoleCommand? found;
            if (!_commands.TryGetValue(name, out found) || found == null)
            {
                return false;
            }
            command = found;
            return true;
        }

        /// <summary>
        /// Executa uma linha (`/coop tp on`, `!dummy`, `stop`): remove o
        /// prefixo, separa nome+args e despacha. Com sessao, injeta via
        /// ISessionTarget (ex. help responde no chat do solicitante).
        /// </summary>
        public bool ExecuteLine(string line, Session.Session? session, out string response)
        {
            response = string.Empty;
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }
            string text = line.Trim();
            if (text.StartsWith("/", StringComparison.Ordinal) || text.StartsWith("!", StringComparison.Ordinal))
            {
                text = text.Substring(1);
            }
            string[] parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }
            ConsoleCommand? command;
            if (!TryGet(parts[0], out command) || command == null)
            {
                response = "Comando '" + parts[0] + "' nao encontrado! Digite /help para ver os comandos.";
                return false;
            }
            var args = new List<string>();
            for (int i = 1; i < parts.Length; i++)
            {
                args.Add(parts[i]);
            }
            if (command is ISessionTarget target)
            {
                target.TargetSession = session;
            }
            try
            {
                return command.Execute(args, out response);
            }
            catch (Exception ex)
            {
                _context.Log.Log(ServerLogLevel.Error, "CMD", "Comando '" + parts[0] + "' falhou: " + ex.Message);
                response = "Falha ao executar '" + parts[0] + "': " + ex.Message;
                return false;
            }
        }
    }
}
