using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using OriCoop;
using OriCoopDedicatedServer.Net.Diagnostics;

namespace OriCoopDedicatedServer.Net.Game
{
    /// <summary>
    /// Configuracao de gameplay do novo core (D-13/D-16): instancia injetavel,
    /// sem estatico global mutavel. Guarda os 8 bools do CONFIG_SYNC 16 nesta
    /// ordem exata: AllowTeleport, ShareAbilities, ShareStoryOnly,
    /// ShareWorldEvents, ShareDoorsAndLevers, ShowNicknames, ClientColors,
    /// EntitySync (os 6 primeiros preservam ordem/legado; os 2 ultimos
    /// absorvem as netvars "cc"/"ES" do pacote -3 morto). Persiste em
    /// serverconfig.json ao lado do exe (load no start, save a cada mudanca)
    /// e NUNCA zera as opcoes no boot (o OnEnable antigo zerava — proibido).
    /// </summary>
    public sealed class ConfigStore
    {
        public const string FileName = "serverconfig.json";

        /// <summary>Ordem canonica dos 8 bools no fio e no JSON.</summary>
        public static readonly string[] FlagNames = new string[]
        {
            "AllowTeleport",
            "ShareAbilities",
            "ShareStoryOnly",
            "ShareWorldEvents",
            "ShareDoorsAndLevers",
            "ShowNicknames",
            "ClientColors",
            "EntitySync",
        };

        private readonly object _sync = new object();
        private readonly ILogger _log;
        private readonly string _filePath;
        private readonly bool[] _flags = new bool[8];
        private readonly Dictionary<int, byte[]> _colors = new Dictionary<int, byte[]>();
        private readonly Random _random = new Random();

        /// <summary>Raised (fora do lock) apos cada mudanca aplicada.</summary>
        public event Action? Changed;

        public ConfigStore(ILogger log)
            : this(log, DefaultPath())
        {
        }

        public ConfigStore(ILogger log, string filePath)
        {
            _log = log ?? throw new ArgumentNullException("log");
            _filePath = string.IsNullOrEmpty(filePath) ? DefaultPath() : filePath;
            ResetToDefaults();
        }

        public bool AllowTeleport { get { lock (_sync) { return _flags[0]; } } }

        public bool ShareAbilities { get { lock (_sync) { return _flags[1]; } } }

        public bool ShareStoryOnly { get { lock (_sync) { return _flags[2]; } } }

        public bool ShareWorldEvents { get { lock (_sync) { return _flags[3]; } } }

        public bool ShareDoorsAndLevers { get { lock (_sync) { return _flags[4]; } } }

        public bool ShowNicknames { get { lock (_sync) { return _flags[5]; } } }

        public bool ClientColors { get { lock (_sync) { return _flags[6]; } } }

        public bool EntitySync { get { lock (_sync) { return _flags[7]; } } }

        public string FilePath
        {
            get { return _filePath; }
        }

        public static string DefaultPath()
        {
            string dir;
            try
            {
                dir = AppContext.BaseDirectory;
            }
            catch (Exception)
            {
                dir = Directory.GetCurrentDirectory();
            }
            if (string.IsNullOrEmpty(dir))
            {
                dir = Directory.GetCurrentDirectory();
            }
            return Path.Combine(dir, FileName);
        }

        private void ResetToDefaults()
        {
            // Padroes do OnEnable legado (preservados; ClientColors era false).
            _flags[0] = true;  // AllowTeleport
            _flags[1] = false; // ShareAbilities
            _flags[2] = false; // ShareStoryOnly
            _flags[3] = false; // ShareWorldEvents
            _flags[4] = false; // ShareDoorsAndLevers
            _flags[5] = false; // ShowNicknames
            _flags[6] = false; // ClientColors
            _flags[7] = false; // EntitySync
        }

        /// <summary>
        /// Load no start: arquivo ausente cria um com os padroes; corrompido
        /// mantem os padroes em memoria SEM sobrescrever (proxima mudanca
        /// explicita regrava um JSON limpo). Nunca zera opcoes salvas.
        /// </summary>
        public void Load()
        {
            bool created = false;
            bool[] loaded;
            lock (_sync)
            {
                if (!File.Exists(_filePath))
                {
                    ResetToDefaults();
                    created = true;
                }
                else if (!TryReadFile(out loaded))
                {
                    ResetToDefaults();
                    _log.Log(ServerLogLevel.Warning, "CONFIG",
                        "serverconfig.json ilegivel; usando padroes em memoria (arquivo preservado ate a proxima mudanca)");
                    return;
                }
                else
                {
                    Array.Copy(loaded, _flags, 8);
                }
            }
            if (created)
            {
                Save();
                _log.Log(ServerLogLevel.Info, "CONFIG", "serverconfig.json criado com padroes em " + _filePath);
            }
            else
            {
                _log.Log(ServerLogLevel.Info, "CONFIG", "Opcoes carregadas de " + _filePath + ": " + Describe());
            }
        }

        private bool TryReadFile(out bool[] flags)
        {
            flags = new bool[8];
            try
            {
                string json = File.ReadAllText(_filePath, Encoding.UTF8);
                var dto = JsonSerializer.Deserialize<ConfigDto>(json);
                if (dto == null)
                {
                    return false;
                }
                bool[] defaults = new bool[] { true, false, false, false, false, false, false, false };
                bool?[] read = new bool?[]
                {
                    dto.AllowTeleport, dto.ShareAbilities, dto.ShareStoryOnly,
                    dto.ShareWorldEvents, dto.ShareDoorsAndLevers, dto.ShowNicknames,
                    dto.ClientColors, dto.EntitySync,
                };
                for (int i = 0; i < 8; i++)
                {
                    bool? value = read[i];
                    flags[i] = value.HasValue ? value.Value : defaults[i];
                }
                return true;
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "CONFIG", "Falha ao ler serverconfig.json: " + ex.GetType().Name);
                return false;
            }
        }

        private void Save()
        {
            bool[] snapshot = Snapshot();
            try
            {
                var dto = new ConfigDto
                {
                    AllowTeleport = snapshot[0],
                    ShareAbilities = snapshot[1],
                    ShareStoryOnly = snapshot[2],
                    ShareWorldEvents = snapshot[3],
                    ShareDoorsAndLevers = snapshot[4],
                    ShowNicknames = snapshot[5],
                    ClientColors = snapshot[6],
                    EntitySync = snapshot[7],
                };
                string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Error, "CONFIG", "Falha ao salvar serverconfig.json: " + ex.GetType().Name);
            }
        }

        public bool[] Snapshot()
        {
            lock (_sync)
            {
                bool[] copy = new bool[8];
                Array.Copy(_flags, copy, 8);
                return copy;
            }
        }

        public string Describe()
        {
            bool[] s = Snapshot();
            var sb = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(FlagNames[i]);
                sb.Append('=');
                sb.Append(s[i] ? "on" : "off");
            }
            return sb.ToString();
        }

        private void SetFlag(int index, bool value)
        {
            bool changed;
            lock (_sync)
            {
                changed = _flags[index] != value;
                if (changed)
                {
                    _flags[index] = value;
                }
            }
            if (!changed)
            {
                return;
            }
            Save();
            _log.Log(ServerLogLevel.Info, "CONFIG", FlagNames[index] + " -> " + (value ? "ATIVADO" : "DESATIVADO"));
            try
            {
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                _log.Log(ServerLogLevel.Warning, "CONFIG", "Listener de mudanca falhou: " + ex.GetType().Name);
            }
        }

        public void SetAllowTeleport(bool value) { SetFlag(0, value); }

        public void SetShareAbilities(bool value) { SetFlag(1, value); }

        public void SetShareStoryOnly(bool value) { SetFlag(2, value); }

        public void SetShareWorldEvents(bool value) { SetFlag(3, value); }

        public void SetShareDoorsAndLevers(bool value) { SetFlag(4, value); }

        public void SetShowNicknames(bool value) { SetFlag(5, value); }

        public void SetClientColors(bool value) { SetFlag(6, value); }

        public void SetEntitySync(bool value) { SetFlag(7, value); }

        /// <summary>
        /// Corpo do CONFIG_SYNC 16: marcador int 16 + 8 bools (1 byte cada),
        /// mesma ordem do cliente (BinaryWriter.Write(bool)).
        /// </summary>
        public byte[] BuildConfigPayload()
        {
            bool[] s = Snapshot();
            byte[] payload = new byte[4 + 8];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), (int)PacketType.CONFIG_SYNC);
            for (int i = 0; i < 8; i++)
            {
                payload[4 + i] = s[i] ? (byte)1 : (byte)0;
            }
            return payload;
        }

        /// <summary>
        /// Leitura defensiva do CONFIG_SYNC (T-02-08): exige marcador 16 e
        /// ao menos 8 bytes de flags; ignora bytes extras.
        /// </summary>
        public static bool TryParseConfigPayload(byte[] payload, out bool[] flags)
        {
            flags = new bool[8];
            if (payload == null || payload.Length < 12)
            {
                return false;
            }
            if (BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)) != (int)PacketType.CONFIG_SYNC)
            {
                return false;
            }
            for (int i = 0; i < 8; i++)
            {
                flags[i] = payload[4 + i] != 0;
            }
            return true;
        }

        /// <summary>
        /// Cor por jogador (inclui o dummy 999): primeira chamada sorteia e
        /// guarda; nunca perde a cor ja atribuida (era _ClientColorsDB).
        /// </summary>
        public byte[] GetOrCreateColor(int id)
        {
            lock (_sync)
            {
                byte[]? rgb;
                if (_colors.TryGetValue(id, out rgb) && rgb != null && rgb.Length == 3)
                {
                    return new byte[] { rgb[0], rgb[1], rgb[2] };
                }
                byte[] fresh = new byte[]
                {
                    (byte)_random.Next(50, 256),
                    (byte)_random.Next(50, 256),
                    (byte)_random.Next(50, 256),
                };
                _colors[id] = fresh;
                return new byte[] { fresh[0], fresh[1], fresh[2] };
            }
        }

        public void SetColor(int id, byte r, byte g, byte b)
        {
            lock (_sync)
            {
                _colors[id] = new byte[] { r, g, b };
            }
        }

        private sealed class ConfigDto
        {
            public bool? AllowTeleport { get; set; }

            public bool? ShareAbilities { get; set; }

            public bool? ShareStoryOnly { get; set; }

            public bool? ShareWorldEvents { get; set; }

            public bool? ShareDoorsAndLevers { get; set; }

            public bool? ShowNicknames { get; set; }

            public bool? ClientColors { get; set; }

            public bool? EntitySync { get; set; }
        }
    }
}
