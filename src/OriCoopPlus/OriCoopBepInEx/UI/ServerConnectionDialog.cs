using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using OriCoop;
using OriCoopBepInEx.Plugin;
using UnityEngine;

namespace OriCoopBepInEx.UI
{
    public sealed class ServerConnectionDialog : MonoBehaviour
    {
        public static ServerConnectionDialog Instance { get; private set; }

        public bool IsOpen { get; private set; }

        private string _inputHost = "127.0.0.1";
        private string _inputPort = "7777";
        private string _inputNick = "Ori_Player";
        private string _statusFeedback = string.Empty;
        private bool _isScanningLan = false;

        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _buttonPrimaryStyle;
        private GUIStyle _buttonSecondaryStyle;
        private GUIStyle _statusStyle;
        private Texture2D _windowBgTex;
        private Texture2D _dimTex;
        private Texture2D _btnPrimaryTex;
        private Texture2D _btnSecondaryTex;

        public static void Initialize()
        {
            if (Instance != null)
            {
                return;
            }

            GameObject dialogObj = new GameObject("OriCoop_ServerConnectionDialog");
            UnityEngine.Object.DontDestroyOnLoad(dialogObj);
            Instance = dialogObj.AddComponent<ServerConnectionDialog>();
        }

        public void Open()
        {
            if (OriCoopPlugin.Instance != null)
            {
                _inputHost = OriCoopPlugin.Instance.ServerHost;
                _inputPort = OriCoopPlugin.Instance.ServerPort.ToString();
                _inputNick = OriCoopPlugin.Instance.Nickname;
            }
            _statusFeedback = string.Empty;
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F6))
            {
                Toggle();
            }

            if (IsOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            EnsureStyles();

            // 1. Escurecimento do fundo (Backdrop Dim)
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, new GUIStyle { normal = { background = _dimTex } });

            // 2. Janela Modal Centralizada
            float winWidth = 480f;
            float winHeight = 360f;
            float winX = (Screen.width - winWidth) * 0.5f;
            float winY = (Screen.height - winHeight) * 0.5f;
            Rect winRect = new Rect(winX, winY, winWidth, winHeight);

            GUI.Box(winRect, GUIContent.none, _windowStyle);

            float padX = 24f;
            float curY = winY + 18f;
            float contentWidth = winWidth - (padX * 2f);

            // Titulo
            GUI.Label(new Rect(winX + padX, curY, contentWidth, 26f), "ORI COOP PLUS | CONEXAO COM SERVIDOR", _headerStyle);
            curY += 34f;

            // Status atual
            string statusLine;
            bool isConnected = OriCoopPlugin.Instance != null && OriCoopPlugin.Instance.IsConnected;
            if (isConnected)
            {
                _statusFeedback = string.Empty;
                statusLine = string.Format("<color=#00ff88>CONECTADO</color> (ID: {0} | Ping: {1} ms | Parceiros: {2})",
                    OriCoopPlugin.Instance.AssignedPlayerId,
                    OriCoopPlugin.Instance.CurrentPing < 0 ? "--" : OriCoopPlugin.Instance.CurrentPing.ToString(),
                    OriCoopPlugin.Instance.ConnectedPlayerCount);
            }
            else if (_isScanningLan)
            {
                statusLine = "<color=#ffea00>PROCURANDO SERVIDOR NA REDE LOCAL (LAN)...</color>";
            }

            else if (!string.IsNullOrEmpty(_statusFeedback))
            {
                statusLine = _statusFeedback;
            }
            else
            {
                statusLine = "<color=#ff5555>DESCONECTADO</color> (Aguardando conexao ao servidor)";
            }
            GUI.Label(new Rect(winX + padX, curY, contentWidth, 22f), statusLine, _statusStyle);
            curY += 30f;

            // Campo Host / IP
            GUI.Label(new Rect(winX + padX, curY, contentWidth, 18f), "Endereco do Servidor (IPv4 ou Host):", _labelStyle);
            curY += 20f;
            _inputHost = GUI.TextField(new Rect(winX + padX, curY, contentWidth, 26f), _inputHost ?? string.Empty, _textFieldStyle);
            curY += 34f;

            // Campo Porta
            GUI.Label(new Rect(winX + padX, curY, contentWidth, 18f), "Porta UDP (Padrao: 7777):", _labelStyle);
            curY += 20f;
            _inputPort = GUI.TextField(new Rect(winX + padX, curY, contentWidth, 26f), _inputPort ?? string.Empty, _textFieldStyle);
            curY += 34f;

            // Campo Nickname com botao Salvar Nome
            GUI.Label(new Rect(winX + padX, curY, contentWidth, 18f), "Seu Apelido / Nickname:", _labelStyle);
            curY += 20f;
            float nickFieldW = contentWidth - 110f;
            _inputNick = GUI.TextField(new Rect(winX + padX, curY, nickFieldW, 26f), _inputNick ?? string.Empty, _textFieldStyle);
            if (GUI.Button(new Rect(winX + padX + nickFieldW + 8f, curY, 102f, 26f), "Salvar Nome", _buttonSecondaryStyle))
            {
                if (OriCoopPlugin.Instance != null && !string.IsNullOrEmpty(_inputNick))
                {
                    OriCoopPlugin.Instance.SetNickname(_inputNick.Trim());
                    _statusFeedback = "<color=#00ff88>Apelido salvo com sucesso!</color>";
                }
            }
            curY += 38f;

            // Linha de Botoes
            float btnW = (contentWidth - 16f) / 3f;
            string connectBtnLabel = isConnected ? "Reconectar" : "Conectar";

            if (GUI.Button(new Rect(winX + padX, curY, btnW, 32f), connectBtnLabel, _buttonPrimaryStyle))
            {
                PerformConnect();
            }

            if (GUI.Button(new Rect(winX + padX + btnW + 8f, curY, btnW, 32f), "Buscar LAN", _buttonSecondaryStyle))
            {
                PerformLanScan();
            }

            if (GUI.Button(new Rect(winX + padX + (btnW + 8f) * 2f, curY, btnW, 32f), "Fechar", _buttonSecondaryStyle))
            {
                Close();
            }
            curY += 40f;

            // Desconectar se estiver conectado
            if (isConnected)
            {
                if (GUI.Button(new Rect(winX + padX, curY, contentWidth, 24f), "Desconectar do Servidor", _buttonSecondaryStyle))
                {
                    OriCoopPlugin.Instance.Disconnect();
                    _statusFeedback = "<color=#ffaa00>Desconectado com sucesso.</color>";
                }
            }
        }

        private void PerformConnect()
        {
            int port;
            if (!int.TryParse(_inputPort, out port) || port < 1 || port > 65535)
            {
                _statusFeedback = "<color=#ff5555>Erro: Porta invalida (deve ser entre 1 e 65535).</color>";
                return;
            }

            string host = string.IsNullOrEmpty(_inputHost) ? "127.0.0.1" : _inputHost.Trim();
            string nick = string.IsNullOrEmpty(_inputNick) ? "Ori_Player" : _inputNick.Trim();

            if (OriCoopPlugin.Instance != null)
            {
                _statusFeedback = string.Format("<color=#00e5ff>Conectando a {0}:{1} como '{2}'...</color>", host, port, nick);
                OriCoopPlugin.Instance.Connect(host, port, nick);
            }
        }

        private void PerformLanScan()
        {
            if (_isScanningLan)
            {
                return;
            }

            int port;
            if (!int.TryParse(_inputPort, out port) || port < 1 || port > 65535)
            {
                port = 7777;
                _inputPort = "7777";
            }

            _isScanningLan = true;
            _statusFeedback = "<color=#ffea00>Sondando localhost e broadcast da LAN na porta " + port + "...</color>";

            ThreadPool.QueueUserWorkItem(delegate
            {
                string foundHost = null;
                try
                {
                    using (UdpClient probe = new UdpClient())
                    {
                        probe.EnableBroadcast = true;
                        probe.Client.ReceiveTimeout = 600;

                        // Sonda no envelope versionado (D-02): Hello pre-handshake
                        // (clientId -1, token 0). O servidor novo derruba a sonda
                        // legada de 4 bytes sem resposta, entao o scan precisa
                        // falar o protocolo atual: header 24B + versao + nick.
                        byte[] probePacket = BuildLanProbeHello();

                        // Envia para localhost e broadcast
                        probe.Send(probePacket, probePacket.Length, new IPEndPoint(IPAddress.Loopback, port));
                        probe.Send(probePacket, probePacket.Length, new IPEndPoint(IPAddress.Broadcast, port));

                        // Aceita ate 2 respostas (localhost + LAN); a primeira
                        // Welcome (101) ou Reject (106) valida o servidor novo.
                        // Reject tambem conta como "encontrado": ha um servidor
                        // do build atual la, mesmo que cheio/incompativel.
                        long deadlineTicks = DateTime.UtcNow.Ticks + TimeSpan.TicksPerMillisecond * 1200;
                        while (foundHost == null && DateTime.UtcNow.Ticks < deadlineTicks)
                        {
                            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                            byte[] response;
                            try
                            {
                                response = probe.Receive(ref remoteEP);
                            }
                            catch (SocketException)
                            {
                                break;
                            }
                            if (response != null && IsNewCoreServerReply(response))
                            {
                                foundHost = remoteEP.Address.ToString();
                            }
                        }
                    }
                }
                catch
                {
                    // Timeout ou porta fechada
                }

                OriCoopPlugin.EnqueueMainThread(delegate
                {
                    _isScanningLan = false;
                    if (!string.IsNullOrEmpty(foundHost))
                    {
                        _inputHost = foundHost;
                        _statusFeedback = string.Format("<color=#00ff88>Servidor detectado em {0}:{1}!</color>", foundHost, port);
                    }
                    else
                    {
                        _statusFeedback = "<color=#ff9999>Nenhum servidor respondeu na porta " + port + ".</color>";
                    }
                });
            });
        }

        private static byte[] BuildLanProbeHello()
        {
            using (MemoryStream packet = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(packet))
            {
                writer.Write(NetProtocol.Magic);
                writer.Write(NetProtocol.Version);
                writer.Write((byte)0);
                writer.Write((uint)0);
                writer.Write(NetProtocol.PreHandshakeId);
                writer.Write((uint)0);
                writer.Write(NetProtocol.MsgHello);
                writer.Write((uint)0);
                writer.Write(NetProtocol.Version);
                WriteProbeString(writer, "lanprobe");
                writer.Flush();
                return packet.ToArray();
            }
        }

        private static bool IsNewCoreServerReply(byte[] datagram)
        {
            if (datagram == null || datagram.Length < NetProtocol.HeaderSize)
            {
                return false;
            }
            using (MemoryStream stream = new MemoryStream(datagram))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                ushort magic = reader.ReadUInt16();
                byte version = reader.ReadByte();
                reader.ReadByte(); // flags
                reader.ReadUInt32(); // seq
                reader.ReadInt32(); // clientId
                reader.ReadUInt32(); // token
                int packetId = reader.ReadInt32();
                if (magic != NetProtocol.Magic || version != NetProtocol.Version)
                {
                    return false;
                }
                return packetId == NetProtocol.MsgWelcome || packetId == NetProtocol.MsgReject;
            }
        }

        private static void WriteProbeString(BinaryWriter writer, string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
            {
                return;
            }

            _dimTex = MakeSolidTexture(new Color(0f, 0f, 0f, 0.65f));
            _windowBgTex = MakeBorderedTexture(new Color(0.04f, 0.08f, 0.14f, 0.95f), new Color(0.12f, 0.64f, 0.95f, 0.9f));
            _btnPrimaryTex = MakeBorderedTexture(new Color(0.08f, 0.42f, 0.65f, 0.95f), new Color(0.35f, 0.85f, 1f, 1f));
            _btnSecondaryTex = MakeBorderedTexture(new Color(0.1f, 0.16f, 0.24f, 0.9f), new Color(0.25f, 0.45f, 0.65f, 0.8f));

            _windowStyle = new GUIStyle();
            _windowStyle.normal.background = _windowBgTex;

            _headerStyle = new GUIStyle();
            _headerStyle.fontSize = 14;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = new Color(0.35f, 0.88f, 1f);
            _headerStyle.alignment = TextAnchor.MiddleCenter;

            _statusStyle = new GUIStyle();
            _statusStyle.fontSize = 11;
            _statusStyle.richText = true;
            _statusStyle.normal.textColor = Color.white;
            _statusStyle.alignment = TextAnchor.MiddleCenter;

            _labelStyle = new GUIStyle();
            _labelStyle.fontSize = 11;
            _labelStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);

            _textFieldStyle = new GUIStyle(GUI.skin.textField);
            _textFieldStyle.fontSize = 12;
            _textFieldStyle.normal.textColor = Color.white;
            _textFieldStyle.focused.textColor = Color.cyan;

            _buttonPrimaryStyle = new GUIStyle();
            _buttonPrimaryStyle.normal.background = _btnPrimaryTex;
            _buttonPrimaryStyle.normal.textColor = Color.white;
            _buttonPrimaryStyle.fontStyle = FontStyle.Bold;
            _buttonPrimaryStyle.fontSize = 12;
            _buttonPrimaryStyle.alignment = TextAnchor.MiddleCenter;

            _buttonSecondaryStyle = new GUIStyle();
            _buttonSecondaryStyle.normal.background = _btnSecondaryTex;
            _buttonSecondaryStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);
            _buttonSecondaryStyle.fontSize = 11;
            _buttonSecondaryStyle.alignment = TextAnchor.MiddleCenter;
        }

        private static Texture2D MakeSolidTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeBorderedTexture(Color fill, Color border)
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                    {
                        tex.SetPixel(x, y, border);
                    }
                    else
                    {
                        tex.SetPixel(x, y, fill);
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}
