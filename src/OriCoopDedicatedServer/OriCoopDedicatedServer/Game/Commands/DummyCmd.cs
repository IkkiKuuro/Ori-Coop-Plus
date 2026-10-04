using System;
using System.Collections.Generic;
using OriCoopDedicatedServer.Core.CommandSystem;

namespace OriCoopDedicatedServer.Game.Commands
{
    public class DummyCmd : ConsoleCommand
    {
        public string Command => "dummy";
        public string[] Aliases => new string[] { "bot", "testbot" };
        public string Description => "Control the Coop Test Dummy Bot (/dummy [spawn|despawn|ability|lever|door|status])";

        public bool Execute(List<string> arguments, out string response)
        {
            if (arguments.Count == 0)
            {
                DummyManager.Toggle();
                response = $"Test Dummy bot is now: {(DummyManager.IsActive ? "ACTIVE (ID 999)" : "INACTIVE")}";
                return true;
            }

            string sub = arguments[0].ToLower();
            switch (sub)
            {
                case "spawn":
                case "start":
                case "on":
                    DummyManager.Spawn();
                    response = "Dummy bot SPAWNED (ID 999, Nick 'Bot_Amigo').";
                    return true;

                case "despawn":
                case "stop":
                case "off":
                    DummyManager.Despawn();
                    response = "Dummy bot DESPAWNED.";
                    return true;

                case "status":
                    response = $"Dummy Active: {DummyManager.IsActive}, Pos: ({DummyManager.DummyPosition.X:F1}, {DummyManager.DummyPosition.Y:F1}, {DummyManager.DummyPosition.Z:F1})";
                    return true;

                case "ability":
                case "skill":
                    if (arguments.Count < 2)
                    {
                        response = "Uso: /dummy ability <DoubleJump|Bash|Stomp|WallJump|Climb|Glide|Dash|Grenade|WaterBreath|ChargeJump>";
                        return false;
                    }
                    string abName = arguments[1];
                    int abId = ParseAbility(abName);
                    DummyManager.TriggerAbility(abId, abName);
                    response = $"Triggered ability '{abName}' (ID {abId}) from Dummy bot to all players!";
                    return true;

                case "lever":
                    int dir = 0; // left = 0, middle = 1, right = 2
                    if (arguments.Count >= 2 && arguments[1].ToLower() == "right")
                    {
                        dir = 2;
                    }
                    DummyManager.TriggerLever(dir);
                    response = $"Triggered test lever ({arguments[1] ?? "left"})!";
                    return true;

                case "door":
                    DummyManager.TriggerDoor();
                    response = "Triggered test door open!";
                    return true;

                default:
                    response = "Subcomandos disponÃ­veis: spawn, despawn, status, ability <nome>, lever <left/right>, door";
                    return false;
            }
        }

        private int ParseAbility(string name)
        {
            switch (name.ToLower())
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
                    if (int.TryParse(name, out int id)) return id;
                    return 5; // Default DoubleJump
            }
        }
    }
}

