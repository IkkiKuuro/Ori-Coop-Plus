using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OriCoopDedicatedServer.Core.CommandSystem;
using OriCoopDedicatedServer.Net.Session;

namespace OriCoopDedicatedServer.Net.Game.Commands
{
    /// <summary>
    /// Os sete comandos do operador sobre o novo core (D-13/D-16): mesma
    /// semantica e mesma interface textual dos comandos antigos (coop, tp,
    /// dummy, clientcolors, entitysync, help, stop), mas como instancias sobre
    /// IServerContext — nenhum estatico global (Server/ServerSend) e
    /// referenciado. Toggle de qualquer opcao persiste em serverconfig.json e
    /// dispara CONFIG_SYNC via o evento Changed do ConfigStore. Registro via
    /// <see cref="RegisterAll"/>; a fiacao no loop de console e do cutover
    /// (02-04).
    /// </summary>
    public static class OriCommands
    {
        public static void RegisterAll(CommandRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException("registry");
            }
            IServerContext ctx = registry.Context;
            registry.Register(new CoopCommand(ctx));
            registry.Register(new TeleportCommand(ctx));
            registry.Register(new DummyCommand(ctx));
            registry.Register(new ClientColorsCommand(ctx));
            registry.Register(new EntitySyncCommand(ctx));
            registry.Register(new HelpCommand(ctx));
            registry.Register(new StopCommand(ctx));
        }

        private static bool ParseToggle(List<string> arguments, int index, out bool value)
        {
            value = false;
            if (arguments.Count <= index)
            {
                return false;
            }
            string arg = arguments[index].ToLowerInvariant();
            if (arg == "on" || arg == "true" || arg == "1" || arg == "sim")
            {
                value = true;
                return true;
            }
            if (arg == "off" || arg == "false" || arg == "0" || arg == "nao")
            {
                value = false;
                return true;
            }
            return false;
        }

        private sealed class CoopCommand : ConsoleCommand
        {
            private readonly IServerContext _ctx;

            public CoopCommand(IServerContext ctx)
            {
                _ctx = ctx ?? throw new ArgumentNullException("ctx");
            }

            public string Command { get { return "coop"; } }

            public string[] Aliases { get { return new string[] { "coopconfig", "config", "cfg" }; } }

            public string Description { get { return "Configure and toggle Ori Coop Plus features (/coop [tp|abilities|story|world|doors|names] [on|off])"; } }

            public bool Execute(List<string> arguments, out string response)
            {
                ConfigStore config = _ctx.Config;
                if (arguments.Count == 0)
                {
                    response = "\n=== Ori Coop Plus - Configuracoes Atuais ===" +
                        "\n [1] Teleporte (/coop tp): " + (config.AllowTeleport ? "ATIVADO" : "DESATIVADO") +
                        "\n [2] Compartilhar Habilidades (/coop abilities): " + (config.ShareAbilities ? "ATIVADO" : "DESATIVADO") +
                        "\n [3] Apenas Habilidades de Historia (/coop story): " + (config.ShareStoryOnly ? "ATIVADO" : "DESATIVADO") +
                        "\n [4] Eventos do Mundo (/coop world): " + (config.ShareWorldEvents ? "ATIVADO" : "DESATIVADO") +
                        "\n [5] Portas e Alavancas (/coop doors): " + (config.ShareDoorsAndLevers ? "ATIVADO" : "DESATIVADO") +
                        "\n [6] Nomes Flutuantes (/coop names): " + (config.ShowNicknames ? "ATIVADO" : "DESATIVADO") +
                        "\n============================================\nPara alterar: /coop <opcao> [on/off]";
                    return true;
                }

                string opt = arguments[0].ToLowerInvariant();
                bool targetState;
                bool hasTarget = ParseToggle(arguments, 1, out targetState);

                switch (opt)
                {
                    case "tp":
                    case "teleport":
                        config.SetAllowTeleport(hasTarget ? targetState : !config.AllowTeleport);
                        response = "Teleporte configurado para: " + (config.AllowTeleport ? "ATIVADO" : "DESATIVADO");
                        return true;

                    case "abilities":
                    case "ability":
                    case "skills":
                        config.SetShareAbilities(hasTarget ? targetState : !config.ShareAbilities);
                        response = "Compartilhar Habilidades configurado para: " + (config.ShareAbilities ? "ATIVADO" : "DESATIVADO");
                        return true;

                    case "story":
                    case "storyonly":
                        config.SetShareStoryOnly(hasTarget ? targetState : !config.ShareStoryOnly);
                        response = "Apenas Habilidades de Historia configurado para: " + (config.ShareStoryOnly ? "ATIVADO" : "DESATIVADO");
                        return true;

                    case "world":
                    case "events":
                        config.SetShareWorldEvents(hasTarget ? targetState : !config.ShareWorldEvents);
                        response = "Eventos de Mundo configurado para: " + (config.ShareWorldEvents ? "ATIVADO" : "DESATIVADO");
                        return true;

                    case "doors":
                    case "levers":
                    case "door":
                    case "lever":
                        config.SetShareDoorsAndLevers(hasTarget ? targetState : !config.ShareDoorsAndLevers);
                        response = "Portas e Alavancas configurado para: " + (config.ShareDoorsAndLevers ? "ATIVADO" : "DESATIVADO");
                        return true;

                    case "names":
                    case "nick":
                    case "nicks":
                    case "nicknames":
                        config.SetShowNicknames(hasTarget ? targetState : !config.ShowNicknames);
                        response = "Nomes Flutuantes configurado para: " + (config.ShowNicknames ? "ATIVADO" : "DESATIVADO");
                        return true;

                    default:
                        response = "Opcao invalida. Use: tp, abilities, story, world, doors, names";
                        return false;
                }
            }
        }

        private sealed class TeleportCommand : ConsoleCommand
        {
            private readonly IServerContext _ctx;

            public TeleportCommand(IServerContext ctx)
            {
                _ctx = ctx ?? throw new ArgumentNullException("ctx");
            }

            public string Command { get { return "tp"; } }

            public string[] Aliases { get { return new string[] { "teleport" }; } }

            public string Description { get { return "Teleporta um jogador ate outro (/tp <origem> <destino>)"; } }

            public bool Execute(List<string> arguments, out string response)
            {
                if (!_ctx.Config.AllowTeleport)
                {
                    response = "Teleporte desativado. Use /coop tp on.";
                    return false;
                }

                if (arguments.Count != 2)
                {
                    response = "Uso: /tp <jogador-origem> <jogador-destino>";
                    return false;
                }

                Session.Session? source = _ctx.Game.FindSession(arguments[0]);
                if (source == null)
                {
                    response = "Jogador de origem não encontrado. Use o nick exato ou o ID.";
                    return false;
                }

                float x;
                float y;
                float z;
                string destNick;
                if ((_ctx.Dummy.IsActive && arguments[1] == DummyBot.DummyId.ToString())
                    || string.Equals(arguments[1], DummyBot.DummyNick, StringComparison.OrdinalIgnoreCase))
                {
                    if (!_ctx.Dummy.GetPosition(out x, out y, out z))
                    {
                        response = "Dummy bot inativo no momento.";
                        return false;
                    }
                    destNick = DummyBot.DummyNick;
                }
                else
                {
                    Session.Session? destination = _ctx.Game.FindSession(arguments[1]);
                    if (destination == null)
                    {
                        response = "Jogador de destino não encontrado. Use o nick exato ou o ID.";
                        return false;
                    }
                    if (source.Id == destination.Id)
                    {
                        response = "A origem e o destino precisam ser jogadores diferentes.";
                        return false;
                    }
                    if (!_ctx.Game.TryGetPosition(destination.Id, out x, out y, out z))
                    {
                        string lostNick = destination.Nickname;
                        if (string.IsNullOrEmpty(lostNick))
                        {
                            lostNick = "Jogador " + destination.Id;
                        }
                        response = "Ainda não existe uma posição recebida para " + lostNick + ".";
                        return false;
                    }
                    destNick = destination.Nickname;
                    if (string.IsNullOrEmpty(destNick))
                    {
                        destNick = "Jogador " + destination.Id;
                    }
                }

                try
                {
                    _ctx.Game.SendTeleportAsync(source, x, y, z, destNick, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    string sourceNick = source.Nickname;
                    if (string.IsNullOrEmpty(sourceNick))
                    {
                        sourceNick = "Player " + source.Id;
                    }
                    _ctx.Game.ChatAllAsync("<color=red>SERVER</color>",
                        "<color=cyan>" + sourceNick + "</color> foi teleportado ate <color=cyan>" + destNick + "</color>.",
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    response = "Falha de rede ao teleportar: " + ex.GetType().Name;
                    return false;
                }

                string okNick = source.Nickname;
                if (string.IsNullOrEmpty(okNick))
                {
                    okNick = "Player " + source.Id;
                }
                response = "Teleportando " + okNick + " até " + destNick + ".";
                return true;
            }
        }

        private sealed class DummyCommand : ConsoleCommand
        {
            private readonly IServerContext _ctx;

            public DummyCommand(IServerContext ctx)
            {
                _ctx = ctx ?? throw new ArgumentNullException("ctx");
            }

            public string Command { get { return "dummy"; } }

            public string[] Aliases { get { return new string[] { "bot", "testbot", "fakeplayer", "fakepl", "fp" }; } }

            public string Description { get { return "Control the Coop Test Dummy Bot (/dummy [spawn|despawn|echo|anim|status|ability|lever|door])"; } }

            public bool Execute(List<string> arguments, out string response)
            {
                DummyBot dummy = _ctx.Dummy;
                if (arguments.Count == 0)
                {
                    dummy.Toggle();
                    response = "Test Dummy bot is now: " + (dummy.IsActive ? "ACTIVE (ID 999)" : "INACTIVE");
                    return true;
                }

                string sub = arguments[0].ToLowerInvariant();
                switch (sub)
                {
                    case "spawn":
                    case "start":
                    case "on":
                        dummy.Spawn();
                        response = "Dummy bot SPAWNED (ID 999, Nick 'Bot_Amigo').";
                        return true;

                    case "despawn":
                    case "stop":
                    case "off":
                        dummy.Despawn();
                        response = "Dummy bot DESPAWNED.";
                        return true;

                    case "status":
                        response = dummy.StatusLine();
                        return true;

                    case "echo":
                    case "espelho":
                        if (arguments.Count >= 2)
                        {
                            string echoArg = arguments[1].ToLowerInvariant();
                            if (echoArg == "off" || echoArg == "stop")
                            {
                                dummy.SetEchoMode(false);
                                response = "Dummy modo eco DESATIVADO (parado em Idle).";
                                return true;
                            }
                            if (echoArg == "on" || echoArg == "start")
                            {
                                dummy.SetEchoMode(true);
                                response = "Dummy modo eco ATIVADO — espelha suas anims com ping 20-150 ms.";
                                return true;
                            }
                        }
                        else
                        {
                            dummy.SetEchoMode(!dummy.EchoEnabled);
                            response = "Dummy modo eco: " + (dummy.EchoEnabled ? "ATIVADO" : "DESATIVADO") + ".";
                            return true;
                        }
                        response = "Uso: /dummy echo [on|off]";
                        return false;

                    case "anim":
                    case "mirror":
                        if (arguments.Count < 2)
                        {
                            dummy.SetAnimMode(!dummy.AnimCycleEnabled);
                            response = "Dummy modo espelho: " + (dummy.AnimCycleEnabled ? "ATIVADO" : "DESATIVADO")
                                + " (" + dummy.CurrentModeName() + ")";
                            return true;
                        }
                        string animArg = arguments[1].ToLowerInvariant();
                        if (animArg == "on" || animArg == "start" || animArg == "cycle")
                        {
                            dummy.SetAnimMode(true);
                            response = "Dummy modo espelho ATIVADO — ciclo de 12 estados (2.5 s cada).";
                            return true;
                        }
                        if (animArg == "off" || animArg == "stop")
                        {
                            dummy.SetAnimMode(false);
                            response = "Dummy modo espelho DESATIVADO (parado em Idle).";
                            return true;
                        }
                        if (dummy.LockAnimState(arguments[1]))
                        {
                            response = "Dummy anim travada em " + dummy.CurrentModeName() + ".";
                            return true;
                        }
                        response = "Estado desconhecido. Uso: /dummy anim [on|off|<Idle|Running|Jump|DoubleJump|Falling|WallSlide|WallJump|Bash|Glide|ChargeJump|Stomp|Dash>]";
                        return false;

                    case "ability":
                    case "skill":
                        if (arguments.Count < 2)
                        {
                            response = "Uso: /dummy ability <DoubleJump|Bash|Stomp|WallJump|Climb|Glide|Dash|Grenade|WaterBreath|ChargeJump>";
                            return false;
                        }
                        string abName = arguments[1];
                        int abId = ParseAbility(abName);
                        dummy.TriggerAbility(abId, abName);
                        response = "Triggered ability '" + abName + "' (ID " + abId + ") from Dummy bot to all players!";
                        return true;

                    case "lever":
                        int dir = 0; // left = 0, middle = 1, right = 2
                        if (arguments.Count >= 2 && arguments[1].ToLowerInvariant() == "right")
                        {
                            dir = 2;
                        }
                        dummy.TriggerLever(dir);
                        response = "Triggered test lever (" + (arguments.Count >= 2 ? arguments[1] : "left") + ")!";
                        return true;

                    case "door":
                        dummy.TriggerDoor();
                        response = "Triggered test door open!";
                        return true;

                    default:
                        response = "Subcomandos: spawn, despawn, echo [on|off], anim [on|off|<estado>], status, ability <nome>, lever <left/right>, door";
                        return false;
                }
            }

            private static int ParseAbility(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return 5;
                }
                switch (name.ToLowerInvariant())
                {
                    case "bash": return 0;
                    case "chargeflame": return 2;
                    case "walljump": return 3;
                    case "stomp": return 4;
                    case "doublejump": return 5;
                    case "chargejump": return 8;
                    case "magnet": return 10;
                    case "climb": return 12;
                    case "glide": return 14;
                    case "spiritflame": return 15;
                    case "waterbreath": return 23;
                    case "dash": return 50;
                    case "grenade": return 51;
                    case "chargedash": return 53;
                    case "airdash": return 54;
                    default:
                        int id;
                        if (int.TryParse(name, out id))
                        {
                            return id;
                        }
                        return 5; // Default DoubleJump
                }
            }
        }

        private sealed class ClientColorsCommand : ConsoleCommand
        {
            private readonly IServerContext _ctx;

            public ClientColorsCommand(IServerContext ctx)
            {
                _ctx = ctx ?? throw new ArgumentNullException("ctx");
            }

            public string Command { get { return "clientcolors"; } }

            public string[] Aliases { get { return new string[3] { "cc", "clientc", "ccolors" }; } }

            public string Description { get { return "Enable or Disable client colors"; } }

            public bool Execute(List<string> arguments, out string response)
            {
                bool value = !_ctx.Config.ClientColors;
                bool hasTarget = ParseToggle(arguments, 0, out value);
                _ctx.Config.SetClientColors(hasTarget ? value : !_ctx.Config.ClientColors);
                response = "Cores de clientes: " + (_ctx.Config.ClientColors ? "ATIVADO" : "DESATIVADO");
                return true;
            }
        }

        private sealed class EntitySyncCommand : ConsoleCommand
        {
            private readonly IServerContext _ctx;

            public EntitySyncCommand(IServerContext ctx)
            {
                _ctx = ctx ?? throw new ArgumentNullException("ctx");
            }

            public string Command { get { return "entitysync"; } }

            public string[] Aliases { get { return new string[2] { "es", "sync" }; } }

            public string Description { get { return "Enable or Disable entity sync"; } }

            public bool Execute(List<string> arguments, out string response)
            {
                bool value = !_ctx.Config.EntitySync;
                bool hasTarget = ParseToggle(arguments, 0, out value);
                _ctx.Config.SetEntitySync(hasTarget ? value : !_ctx.Config.EntitySync);
                response = "Sincronizacao de entidades: " + (_ctx.Config.EntitySync ? "ATIVADO" : "DESATIVADO");
                return true;
            }
        }

        private sealed class HelpCommand : ConsoleCommand, ISessionTarget
        {
            private readonly IServerContext _ctx;

            public HelpCommand(IServerContext ctx)
            {
                _ctx = ctx ?? throw new ArgumentNullException("ctx");
            }

            public string Command { get { return "help"; } }

            public string[] Aliases { get { return new string[] { "h", "ajuda", "?" }; } }

            public string Description { get { return "Lista os comandos (no chat do solicitante quando via chat)"; } }

            public Session.Session? TargetSession { get; set; }

            public bool Execute(List<string> arguments, out string response)
            {
                var sb = new StringBuilder("Commands:");
                foreach (string name in PrimaryNames)
                {
                    sb.Append(" /");
                    sb.Append(name);
                }
                string list = sb.ToString();
                Session.Session? target = TargetSession;
                TargetSession = null;
                if (target != null)
                {
                    try
                    {
                        _ctx.Game.ChatToAsync(target, "<color=yellow>SERVER</color>", list, CancellationToken.None)
                            .GetAwaiter().GetResult();
                        response = "Ajuda enviada no chat.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        response = "Falha de rede ao enviar ajuda: " + ex.GetType().Name;
                        return false;
                    }
                }
                response = list;
                return true;
            }

            /// <summary>Nomes primarios na ordem de registro (lista do help).</summary>
            private static readonly string[] PrimaryNames = new string[]
            {
                "coop", "tp", "dummy", "clientcolors", "entitysync", "help", "stop",
            };
        }

        private sealed class StopCommand : ConsoleCommand
        {
            private readonly IServerContext _ctx;

            public StopCommand(IServerContext ctx)
            {
                _ctx = ctx ?? throw new ArgumentNullException("ctx");
            }

            public string Command { get { return "stop"; } }

            public string[] Aliases { get { return new string[] { "quit", "exit", "sair" }; } }

            public string Description { get { return "Encerra o servidor (sinaliza o CancellationToken)"; } }

            public bool Execute(List<string> arguments, out string response)
            {
                try
                {
                    _ctx.RequestStop();
                }
                catch (Exception ex)
                {
                    response = "Falha ao sinalizar parada: " + ex.Message;
                    return false;
                }
                response = "Servidor encerrando...";
                return true;
            }
        }
    }
}
