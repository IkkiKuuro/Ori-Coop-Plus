using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using OriCoopBepInEx.Client;
using OriCoopBepInEx.Domain;
using OriCoopBepInEx.Networking;
using OriCoopBepInEx.UI;
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
        private ConfigEntry<bool> _enableLegacyFloatingHud;
        private ConfigEntry<bool> _animVerbose;
        private readonly Dictionary<int, PlayerSnapshot> _remotePlayers = new Dictionary<int, PlayerSnapshot>();
        private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
        private readonly RemotePlayerManager _remotePlayerManager = new RemotePlayerManager();
        private Vector3Data _localPosition;
        private string _localNick = "Voce";
        private int _pingMs = -1;
        private bool _serverAllowTeleport = true;
        private GUIStyle _hudBox;
        private GUIStyle _hudText;
        private GUIStyle _hudHeader;

        public static void EnqueueMainThread(Action action)
        {
            if (Instance != null && action != null)
            {
                lock (Instance._mainThreadActions)
                {
                    Instance._mainThreadActions.Enqueue(action);
                }
            }
        }

        public string ServerHost
        {
            get { return _serverHost != null ? _serverHost.Value : "127.0.0.1"; }
        }

        public int ServerPort
        {
            get { return _serverPort != null ? _serverPort.Value : 7777; }
        }

        public string Nickname
        {
            get { return _nickname != null ? _nickname.Value : "Ori_Player"; }
        }

        public int AssignedPlayerId
        {
            get { return _playerId != null ? _playerId.Value : -1; }
        }

        public bool IsConnected
        {
            get { return _network != null && _network.IsConnected && AssignedPlayerId >= 0; }
        }

        public int CurrentPing
        {
            get { return _pingMs; }
        }

        public int ConnectedPlayerCount
        {
            get
            {
                lock (_remotePlayers)
                {
                    return _remotePlayers.Count;
                }
            }
        }

        public bool ShowPartnerHp { get; set; }
        public bool ShowNetworkLogs { get; set; }

        public static void LogInfo(string msg)
        {
            if (Instance != null) Instance.Logger.LogInfo(msg);
        }

        public static void LogWarning(string msg)
        {
            if (Instance != null) Instance.Logger.LogWarning(msg);
        }

        public static void LogError(string msg)
        {
            if (Instance != null) Instance.Logger.LogError(msg);
        }

        public static bool IsAnimVerbose()
        {
            return Instance != null && Instance._animVerbose != null && Instance._animVerbose.Value;
        }

        public void Connect(string host, int port, string nick)
        {
            try
            {
                if (string.IsNullOrEmpty(host)) host = "127.0.0.1";
                if (port < 1 || port > 65535) port = 7777;
                if (string.IsNullOrEmpty(nick)) nick = "Ori_Player";

                Disconnect();

                _serverHost.Value = host;
                _serverPort.Value = port;
                _nickname.Value = nick;
                _playerId.Value = -1;
                Config.Save();

                _localNick = nick;
                _pingMs = -1;

                _network = new NetworkService(host, port, -1, nick);
                _network.PlayerSnapshotReceived += OnPlayerSnapshotReceived;
                _network.TeleportRequested += OnTeleportRequested;
                _network.ChatMessageReceived += OnChatMessageReceived;
                _network.EntitySyncChanged += OnEntitySyncChanged;
                _network.PingUpdated += OnPingUpdated;
                _network.IdentityAssigned += OnIdentityAssigned;
                _network.PlayerDisconnected += OnPlayerDisconnected;
                _network.ConfigSyncReceived += OnConfigSyncReceived;
                _network.Start();

                Logger.LogInfo(string.Format("Conectando ao servidor Ori Coop em {0}:{1} como '{2}'...", host, port, nick));
            }
            catch (Exception ex)
            {
                Logger.LogError("Falha ao iniciar conexao com servidor: " + ex.Message);
            }
        }

        public void Disconnect()
        {
            if (_network != null)
            {
                try
                {
                    _network.PlayerSnapshotReceived -= OnPlayerSnapshotReceived;
                    _network.TeleportRequested -= OnTeleportRequested;
                    _network.ChatMessageReceived -= OnChatMessageReceived;
                    _network.EntitySyncChanged -= OnEntitySyncChanged;
                    _network.PingUpdated -= OnPingUpdated;
                    _network.IdentityAssigned -= OnIdentityAssigned;
                    _network.PlayerDisconnected -= OnPlayerDisconnected;
                    _network.ConfigSyncReceived -= OnConfigSyncReceived;
                    _network.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Excecao ao fechar rede anterior: " + ex.Message);
                }
                _network = null;
            }

            lock (_remotePlayers)
            {
                _remotePlayers.Clear();
            }

            if (_remotePlayerManager != null)
            {
                _remotePlayerManager.ClearAll();
            }

            _pingMs = -1;
            _playerId.Value = -1;
            Logger.LogInfo("Conexao de rede desconectada.");
        }

        public void TeleportToNearestPartner()
        {
            if (!_serverAllowTeleport)
            {
                Logger.LogWarning("Teleporte bloqueado: servidor com /coop tp off.");
                UI.NativeUIHelper.ShowToast("[Ori Coop] Teleporte desativado pelo servidor (/coop tp on).", 3.5f);
                return;
            }
            int targetId = FindNearestRemotePlayer();
            if (targetId >= 0 && _network != null)
            {
                _network.SendTeleportRequest(targetId);
                Logger.LogInfo("Pedido de teleporte enviado para o jogador " + targetId + ".");
            }
            else
            {
                Logger.LogWarning("Nenhum parceiro disponivel para teleporte.");
            }
        }

        public void ForceResyncPuppets()
        {
            if (_remotePlayerManager != null)
            {
                _remotePlayerManager.ClearAll();
                Logger.LogInfo("Puppets remotos reiniciados. Solicitando reconstrucao visual via novos snapshots.");
            }
        }

        public void SetVerboseLogging(bool enabled)
        {
            ShowNetworkLogs = enabled;
            Logger.LogInfo("Logs de rede verbosos: " + (enabled ? "LIGADO" : "DESLIGADO"));
        }

        public void SetNickname(string newNick)
        {
            if (string.IsNullOrEmpty(newNick))
            {
                return;
            }

            newNick = newNick.Trim();
            if (_nickname != null)
            {
                _nickname.Value = newNick;
                Config.Save();
            }

            _localNick = newNick;
            if (_network != null)
            {
                _network.SendNicknameUpdate(newNick);
            }
            Logger.LogInfo("Apelido alterado para: " + newNick);
        }

        private void Awake()
        {
            Instance = this;
            _serverHost = Config.Bind("Network", "Host", "127.0.0.1", "UDP server host.");
            _serverPort = Config.Bind("Network", "Port", 7777, "UDP server port.");
            _playerId = Config.Bind("Network", "PlayerId", -1, "Local player identifier; keep -1 for server assignment.");
            _nickname = Config.Bind("Network", "Nickname", "Ori_Player", "Name shown to other players.");
            _enableLegacyFloatingHud = Config.Bind("UI", "EnableLegacyFloatingHud", false, "Habilita o HUD flutuante legado (desativado por padrao em favor da UI nativa no menu de pausa).");
            _animVerbose = Config.Bind("Diagnostics", "AnimVerbose", false, "Loga transicoes de animacao remota ([ANIM]) no LogOutput.log.");

            ServerConnectionDialog.Initialize();

            _network = new NetworkService(_serverHost.Value, _serverPort.Value, _playerId.Value, _nickname.Value);
            _network.PlayerSnapshotReceived += OnPlayerSnapshotReceived;
            _network.TeleportRequested += OnTeleportRequested;
            _network.ChatMessageReceived += OnChatMessageReceived;
            _network.EntitySyncChanged += OnEntitySyncChanged;
            _network.PingUpdated += OnPingUpdated;
            _network.IdentityAssigned += OnIdentityAssigned;
            _network.PlayerDisconnected += OnPlayerDisconnected;
            _network.ConfigSyncReceived += OnConfigSyncReceived;
            _network.Start();

            _harmony = new Harmony("com.ikkikuuro.oricoop");
            _harmony.PatchAll();
            Logger.LogInfo("Ori Coop BepInEx plugin loaded.");
        }

        public void Publish(PlayerSnapshot snapshot)
        {
            if (snapshot != null && _network != null)
            {
                snapshot.PlayerId = _playerId.Value;
                snapshot.Nick = !string.IsNullOrEmpty(_localNick) ? _localNick : (_nickname != null ? _nickname.Value : "Ori_Player");
                _localPosition = snapshot.Position;
                _network.SendPlayerSnapshot(snapshot);
            }
        }

        private void OnPlayerSnapshotReceived(PlayerSnapshot snapshot)
        {
            lock (_remotePlayers)
            {
                // BUG #2/#3: pacotes ANIM chegam sem posicao/nick. Sobrescrever aqui
                // zerava a posicao no HUD e quebrava FindNearestRemotePlayer (teleporte).
                PlayerSnapshot existing;
                bool isAnimOnly = (snapshot.Position.X == 0f && snapshot.Position.Y == 0f && snapshot.Position.Z == 0f)
                    && string.IsNullOrEmpty(snapshot.Nick) && !string.IsNullOrEmpty(snapshot.Animation.Name);
                if (isAnimOnly && _remotePlayers.TryGetValue(snapshot.PlayerId, out existing) && existing != null)
                {
                    existing.Animation = snapshot.Animation;
                    existing.Timestamp = snapshot.Timestamp;
                    snapshot = existing;
                }
                else
                {
                    if (isAnimOnly)
                    {
                        // Sem posicao base ainda: guarda mesmo assim para o manager fundir.
                        _remotePlayers[snapshot.PlayerId] = snapshot;
                    }
                    else
                    {
                        if (_remotePlayers.TryGetValue(snapshot.PlayerId, out existing) && existing != null
                            && !string.IsNullOrEmpty(existing.Animation.Name) && string.IsNullOrEmpty(snapshot.Animation.Name))
                        {
                            snapshot.Animation = existing.Animation;
                        }
                        _remotePlayers[snapshot.PlayerId] = snapshot;
                    }
                }
            }

            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(delegate
                {
                    _remotePlayerManager.HandleSnapshot(snapshot);
                });
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

                    // BUG #3: so trocar transform.position nao bastava — o controlador
                    // de fisica do Sein mantinha velocidade residual e a camera nao
                    // acompanhava, parecendo que "nada aconteceu".
                    Vector3 dest = new UnityEngine.Vector3(position.X, position.Y, position.Z);
                    try
                    {
                        sein.transform.position = dest;

                        Rigidbody rb = sein.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }

                        try
                        {
                            Component seinComp = sein.GetComponent<SeinCharacter>();
                            if (seinComp != null)
                            {
                                System.Reflection.PropertyInfo speedProp =
                                    typeof(SeinCharacter).GetProperty("Speed");
                                if (speedProp != null && speedProp.CanWrite)
                                {
                                    speedProp.SetValue(seinComp, Vector3.zero, null);
                                }
                            }
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning("Falha ao aplicar teleporte: " + ex.Message);
                        return;
                    }

                    EnsureCameraFollowsLocalPlayer();

                    Logger.LogMessage("<color=cyan>SERVER</color>: Teleported to " + destination + ".");
                    UI.NativeUIHelper.ShowToast("[Ori Coop] Teleportado ate " + destination + "!", 3.0f);
                });
            }
        }

        private void OnConfigSyncReceived(bool tp, bool ab, bool story, bool world, bool doors, bool names)
        {
            bool changed = (tp != _serverAllowTeleport);
            _serverAllowTeleport = tp;
            Logger.LogInfo(string.Format("Config do servidor: tp={0} abilities={1} world={2} doors={3} names={4}",
                tp ? "on" : "off", ab ? "on" : "off", world ? "on" : "off", doors ? "on" : "off", names ? "on" : "off"));
            if (changed && !tp)
            {
                lock (_mainThreadActions)
                {
                    _mainThreadActions.Enqueue(delegate
                    {
                        UI.NativeUIHelper.ShowToast("[Ori Coop] Teleporte desativado pelo servidor.", 3.0f);
                    });
                }
            }
        }

        private void OnChatMessageReceived(string sender, string message)
        {
            Logger.LogMessage(sender + ": " + message);

            string toastText = message;
            if (!string.IsNullOrEmpty(message) && message.StartsWith("<color=green>+") && message.EndsWith("</color>"))
            {
                string joinedNick = message.Substring(15, message.Length - 23);
                toastText = string.Format("[Ori Coop] [+] {0} conectou!", joinedNick);
            }

            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(delegate
                {
                    UI.NativeUIHelper.ShowToast(toastText, 3.5f);
                });
            }
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
            _playerId.Value = id;
            if (_nickname != null && !string.IsNullOrEmpty(_nickname.Value))
            {
                _localNick = _nickname.Value;
            }
            else if (!string.IsNullOrEmpty(nick))
            {
                _localNick = nick;
            }

            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(delegate
                {
                    UI.NativeUIHelper.ShowToast(string.Format("[Ori Coop] Conectado com sucesso! (ID: {0})", id), 3.5f);
                });
            }
        }

        private void OnPlayerDisconnected(int playerId)
        {
            string nick = null;
            lock (_remotePlayers)
            {
                PlayerSnapshot snap;
                if (_remotePlayers.TryGetValue(playerId, out snap))
                {
                    nick = snap.Nick;
                }
                _remotePlayers.Remove(playerId);
            }

            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(delegate
                {
                    _remotePlayerManager.RemovePlayer(playerId);
                    EnsureCameraFollowsLocalPlayer();

                    string displayName = string.IsNullOrEmpty(nick) ? ("Jogador " + playerId) : nick;
                    Logger.LogInfo(string.Format("[Ori Coop] [-] {0} (ID: {1}) desconectou.", displayName, playerId));
                    UI.NativeUIHelper.ShowToast(string.Format("[Ori Coop] [-] {0} saiu!", displayName), 3.0f);
                });
            }
        }

        public static void EnsureCameraFollowsLocalPlayer()
        {
            try
            {
                SeinCharacter sein = Game.Characters.Sein;
                if (sein == null)
                {
                    UnityEngine.GameObject seinObj = UnityEngine.GameObject.Find("Characters/Sein") ?? UnityEngine.GameObject.Find("Sein");
                    if (seinObj != null)
                    {
                        sein = seinObj.GetComponent<SeinCharacter>();
                        if (sein != null)
                        {
                            Game.Characters.Sein = sein;
                            Game.Characters.Current = sein;
                        }
                    }
                }

                if (sein != null)
                {
                    if (Game.Characters.Current == null || (Game.Characters.Current as UnityEngine.Component) != sein)
                    {
                        Game.Characters.Current = sein;
                    }

                    GameplayCamera cam = Game.UI.Cameras.Current;
                    if (cam != null && (cam.Target == null || cam.Target != sein.transform))
                    {
                        cam.Target = sein.transform;
                        cam.ChangeTargetToCurrentCharacter();
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("EnsureCameraFollowsLocalPlayer exception: " + ex.Message);
            }
        }

        private void OnGUI()
        {
            if (_enableLegacyFloatingHud == null || !_enableLegacyFloatingHud.Value)
            {
                return;
            }

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
                    try
                    {
                        _mainThreadActions.Dequeue()();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning("Exception executing main thread action: " + ex.Message);
                    }
                }
            }

            // Self-healing watchdog: Ensure Game.Characters.Sein and camera focus are preserved on local player
            EnsureCameraFollowsLocalPlayer();

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.T))
            {
                TeleportToNearestPartner();
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
            Disconnect();
            Instance = null;
        }
    }
}
