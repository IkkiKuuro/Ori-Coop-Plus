using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using OriCoopBepInEx.Domain;
using OriCoopBepInEx.Networking;
using UnityEngine;

namespace OriCoopBepInEx.Plugin
{
    [BepInPlugin("com.ikkikuuro.oricoop", "Ori Coop", "0.1.0")]
    public sealed class OriCoopPlugin : BaseUnityPlugin, IPlayerStateSink
    {
        public static OriCoopPlugin Instance { get; private set; }

        private INetworkService _network;
        private Harmony _harmony;
        private ConfigEntry<string> _serverHost;
        private ConfigEntry<int> _serverPort;
        private ConfigEntry<int> _playerId;
        private ConfigEntry<string> _nickname;
        private readonly Dictionary<int, PlayerSnapshot> _remotePlayers = new Dictionary<int, PlayerSnapshot>();
        private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
        private Vector3Data _localPosition;
        private string _localNick = "Voce";
        private int _pingMs = -1;
        private GUIStyle _hudBox;
        private GUIStyle _hudText;
        private GUIStyle _hudHeader;

        private void Awake()
        {
            Instance = this;
            _serverHost = Config.Bind("Network", "Host", "127.0.0.1", "UDP server host.");
            _serverPort = Config.Bind("Network", "Port", 7777, "UDP server port.");
            _playerId = Config.Bind("Network", "PlayerId", -1, "Local player identifier; keep -1 for server assignment.");
            _nickname = Config.Bind("Network", "Nickname", "Ori_Player", "Name shown to other players.");

            _network = new NetworkService(_serverHost.Value, _serverPort.Value, _playerId.Value, _nickname.Value);
            _network.PlayerSnapshotReceived += OnPlayerSnapshotReceived;
            _network.TeleportRequested += OnTeleportRequested;
            _network.ChatMessageReceived += OnChatMessageReceived;
            _network.EntitySyncChanged += OnEntitySyncChanged;
            _network.PingUpdated += OnPingUpdated;
            _network.IdentityAssigned += OnIdentityAssigned;
            _network.Start();

            _harmony = new Harmony("com.ikkikuuro.oricoop");
            _harmony.PatchAll();
            Logger.LogInfo("Ori Coop BepInEx plugin loaded.");
        }

        public void Publish(PlayerSnapshot snapshot)
        {
            if (snapshot != null)
            {
                snapshot.PlayerId = _playerId.Value;
                _localPosition = snapshot.Position;
                _network.SendPlayerSnapshot(snapshot);
            }
        }

        private void OnPlayerSnapshotReceived(PlayerSnapshot snapshot)
        {
            lock (_remotePlayers)
            {
                _remotePlayers[snapshot.PlayerId] = snapshot;
            }
        }

        private void OnTeleportRequested(Vector3Data position, string destination)
        {
            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(delegate
                {
                    UnityEngine.GameObject sein = UnityEngine.GameObject.Find("Characters/Sein");
                    if (sein == null)
                    {
                        sein = UnityEngine.GameObject.Find("Sein");
                    }
                    if (sein == null)
                    {
                        Logger.LogWarning("Teleport received, but the local Sein object was not found.");
                        return;
                    }

                    sein.transform.position = new UnityEngine.Vector3(position.X, position.Y, position.Z);
                    Logger.LogMessage("<color=cyan>SERVER</color>: Teleported to " + destination + ".");
                });
            }
        }

        private void OnChatMessageReceived(string sender, string message)
        {
            Logger.LogMessage(sender + ": " + message);
        }

        private void OnEntitySyncChanged(bool enabled)
        {
            Logger.LogInfo("Entity synchronization: " + (enabled ? "enabled" : "disabled") + ".");
        }

        private void OnPingUpdated(int ping)
        {
            _pingMs = ping;
        }

        private void OnIdentityAssigned(string nick, int id)
        {
            if (!string.IsNullOrEmpty(nick))
            {
                _localNick = nick;
            }
            _playerId.Value = id;
        }

        private void OnGUI()
        {
            EnsureHudStyles();

            float width = 285f;
            float rowHeight = 22f;
            int rowCount;
            lock (_remotePlayers)
            {
                rowCount = _remotePlayers.Count + 1;
            }

            GUI.Box(new UnityEngine.Rect(12f, 12f, width, 44f + rowCount * rowHeight), string.Empty, _hudBox);
            GUI.Label(new UnityEngine.Rect(22f, 18f, width - 20f, 22f), "ORI COOP PLUS", _hudHeader);
            GUI.Label(new UnityEngine.Rect(22f, 39f, width - 20f, 18f), "JOGADORES", _hudText);

            float y = 59f;
            GUI.Label(new UnityEngine.Rect(22f, y, width - 20f, rowHeight),
                FormatPlayerLine(_localNick, _localPosition, _pingMs), _hudText);
            y += rowHeight;

            lock (_remotePlayers)
            {
                foreach (KeyValuePair<int, PlayerSnapshot> entry in _remotePlayers)
                {
                    PlayerSnapshot player = entry.Value;
                    string nick = string.IsNullOrEmpty(player.Nick)
                        ? "Jogador " + entry.Key
                        : player.Nick;
                    GUI.Label(new UnityEngine.Rect(22f, y, width - 20f, rowHeight),
                        FormatPlayerLine(nick, player.Position, _pingMs), _hudText);
                    y += rowHeight;
                }
            }
        }

        private void EnsureHudStyles()
        {
            if (_hudBox != null)
            {
                return;
            }

            _hudBox = new GUIStyle(GUI.skin.box);
            _hudBox.normal.background = MakeHudBackground();
            _hudText = new GUIStyle(GUI.skin.label);
            _hudText.normal.textColor = UnityEngine.Color.white;
            _hudText.fontSize = 12;
            _hudHeader = new GUIStyle(_hudText);
            _hudHeader.normal.textColor = new UnityEngine.Color(0.35f, 0.9f, 1f);
            _hudHeader.fontStyle = FontStyle.Bold;
        }

        private static string FormatPlayerLine(string nick, Vector3Data position, int ping)
        {
            string pingText = ping < 0 ? "--" : ping + " ms";
            return nick + "  |  " + position.X.ToString("F0") + "," +
                position.Y.ToString("F0") + "," + position.Z.ToString("F0") +
                "  |  " + pingText;
        }

        private static Texture2D MakeHudBackground()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new UnityEngine.Color(0.02f, 0.05f, 0.08f, 0.86f));
            texture.Apply();
            return texture;
        }

        private void Update()
        {
            lock (_mainThreadActions)
            {
                while (_mainThreadActions.Count > 0)
                {
                    _mainThreadActions.Dequeue()();
                }
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.T))
            {
                int targetId = FindNearestRemotePlayer();
                if (targetId >= 0)
                {
                    _network.SendTeleportRequest(targetId);
                }
                else
                {
                    Logger.LogWarning("No remote player is available for teleport.");
                }
            }
        }

        private int FindNearestRemotePlayer()
        {
            lock (_remotePlayers)
            {
                float bestDistance = float.MaxValue;
                int bestId = -1;
                foreach (KeyValuePair<int, PlayerSnapshot> entry in _remotePlayers)
                {
                    float x = entry.Value.Position.X - _localPosition.X;
                    float y = entry.Value.Position.Y - _localPosition.Y;
                    float z = entry.Value.Position.Z - _localPosition.Z;
                    float distance = x * x + y * y + z * z;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestId = entry.Key;
                    }
                }
                return bestId;
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
            if (_network != null)
            {
                _network.PlayerSnapshotReceived -= OnPlayerSnapshotReceived;
                _network.TeleportRequested -= OnTeleportRequested;
                _network.ChatMessageReceived -= OnChatMessageReceived;
                _network.EntitySyncChanged -= OnEntitySyncChanged;
                _network.PingUpdated -= OnPingUpdated;
                _network.IdentityAssigned -= OnIdentityAssigned;
                _network.Dispose();
            }
            Instance = null;
        }
    }
}
