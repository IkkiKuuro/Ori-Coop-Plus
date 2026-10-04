using System;
using OriCoopDedicatedServer.Game.Commands;
using OriCoopDedicatedServer.Core.API;
using OriCoopDedicatedServer.Core.CommandSystem;
using OriCoopDedicatedServer.Core.Network;

namespace OriCoopDedicatedServer.Game
{
    public class OriCoopServerModule : ServerModule
    {
        public static OriCoopServerModule Instance;

        public override void OnEnable()
        {
            base.Name = "Ori and the Blind Forest: DE - Coop Plus Server";
            Instance = this;

            ServerConfig.AllowTeleport = false;
            ServerConfig.ShareAbilities = false;
            ServerConfig.ShareStoryOnly = false;
            ServerConfig.ShareWorldEvents = false;
            ServerConfig.ShareDoorsAndLevers = false;
            ServerConfig.ShowNicknames = false;
            ServerConfig.ClientColors = false;
            ServerConfig.EntitySync = false;

            RegisterHandlers();
            RegisterCommands();

            Info("==========================================");
            Info(" Ori Coop Plus Server Module CARREGADO!");
            Info(" Recursos ativos: Teleporte, Habilidades, Mundo, Nomes.");
            Info(" Digite /coop para ver as opÃ§Ãµes ou /dummy para bot de testes.");
            Info("==========================================");
        }

        private void RegisterCommands()
        {
            CommandProcessor.RegisterCommand(new DummyCmd());
            CommandProcessor.RegisterCommand(new FakePlayerCmd());
            CommandProcessor.RegisterCommand(new CoopConfigCmd());
            CommandProcessor.RegisterCommand(new TeleportCmd());
            CommandProcessor.RegisterCommand(new AllowCustomColorsCmd());
            CommandProcessor.RegisterCommand(new AllowEntitySyncCmd());
        }

        private void RegisterHandlers()
        {
            ServerEvents.OnClientConnected = (Action<Client>)Delegate.Combine(ServerEvents.OnClientConnected, new Action<Client>(NetworkHandler.OnPlayerJoin));
            ServerEvents.OnPacketRecived = (Action<int, Client, Packet>)Delegate.Combine(ServerEvents.OnPacketRecived, new Action<int, Client, Packet>(NetworkHandler.OnPacketRecived));
        }
    }
}
