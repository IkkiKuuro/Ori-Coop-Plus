using System;
using System.Collections.Generic;
using OriCoop;
using OriCoopBepInEx.Diagnostics;
using OriCoopBepInEx.Domain;
using OriCoopBepInEx.Plugin;

namespace OriCoopBepInEx.Events
{
    // Delegate proprio (2 params: sender + DTO). Action<,> com 2 params seria
    // valido em C# 5, mas o delegate nomeado documenta a ordem canonica do
    // evento, no mesmo espirito do ConfigSyncHandler em INetworkService.
    public delegate void PlayerEventHandler(int senderId, SpiritFlameEventData data);

    // Core central de eventos do personagem (fase 3, D-05): patches Harmony
    // so detectam e chamam Publish; o core monta o evento e publica no
    // transporte. O caminho de recepcao (DispatchLocal) nunca publica —
    // puppet/remoto jamais republica (sem eco, D-07).
    public static class PlayerEventCore
    {
        private static readonly Dictionary<byte, PlayerEventHandler> s_handlers = new Dictionary<byte, PlayerEventHandler>();
        private static INetworkService s_transport;

        public static void BindTransport(INetworkService transport)
        {
            s_transport = transport;
        }

        public static void RegisterHandler(byte kind, PlayerEventHandler handler)
        {
            if (handler == null)
            {
                return;
            }
            s_handlers[kind] = handler;
        }

        public static void PublishSpiritFlame(Vector3Data direction, Vector3Data origin, Vector3Data aim, long timestampTicks)
        {
            Publish((byte)PlayerEventKind.SpiritFlame, direction, origin, aim, timestampTicks, "PublishSpiritFlame");
        }

        // Rajada carregada (kind 2, radial no orbe): aim = origin (sem
        // direcao de viagem; o visual remoto e radial, nao beam).
        public static void PublishChargedFlame(Vector3Data origin, long timestampTicks)
        {
            Vector3Data zero = new Vector3Data(0f, 0f, 0f);
            Publish((byte)PlayerEventKind.ChargedFlame, zero, origin, origin, timestampTicks, "PublishChargedFlame");
        }

        private static void Publish(byte kind, Vector3Data direction, Vector3Data origin, Vector3Data aim, long timestampTicks, string tag)
        {
            INetworkService transport = s_transport;
            if (transport == null || !transport.IsConnected)
            {
                return;
            }
            SpiritFlameEventData data = new SpiritFlameEventData(
                kind, direction, origin, aim, timestampTicks);
            try
            {
                transport.SendPlayerEvent(data);
            }
            catch (Exception ex)
            {
                OriCoopPlugin.LogWarning("[EVENT] " + tag + " falhou: " + ex.Message);
                return;
            }
            // Fase enviado (D-12, G-03-3): conta + registra no ring + LogInfo
            // SEMPRE (fora de qualquer gate IsAnimVerbose) — shots sao
            // eventos discretos, volume limitado pela taxa de disparo.
            string sentLine = "[EVENT] kind=spiritflame fase=enviado";
            ReplicationObservability.Record(sentLine);
            ReplicationObservability.TrackPlayerEvent(data.Kind, true);
            OriCoopPlugin.LogInfo(sentLine);
        }

        public static void DispatchLocal(int senderId, byte kind, SpiritFlameEventData data)
        {
            PlayerEventHandler handler;
            if (s_handlers.TryGetValue(kind, out handler) && handler != null)
            {
                string recvLine = string.Format("[EVENT] P{0} kind={1} fase=recebido",
                    senderId, kind);
                ReplicationObservability.Record(recvLine);
                ReplicationObservability.TrackPlayerEvent(kind, true);
                OriCoopPlugin.LogInfo(recvLine);
                try
                {
                    handler(senderId, data);
                }
                catch (Exception ex)
                {
                    OriCoopPlugin.LogWarning("[EVENT] handler kind=" + kind + " falhou: " + ex.Message);
                    // Handler com excecao: descartado com motivo (D-15) —
                    // conta como dropped mesmo fora do modo verboso, para
                    // que falhas silenciosas aparecam nos numeros.
                    ReplicationObservability.TrackPlayerEvent(kind, false);
                }
                return;
            }
            // Evento desconhecido: fail-closed (D-15) — mantem a ultima anim
            // valida, conta + registra + LogInfo SEMPRE (decidivel com
            // config padrao, G-03-3). Nunca publica.
            string unknownLine = string.Format("[EVENT] P{0} kind={1} aplicado=manteve-atual motivo=tipo-desconhecido",
                senderId, kind);
            ReplicationObservability.Record(unknownLine);
            ReplicationObservability.TrackPlayerEvent(kind, false);
            OriCoopPlugin.LogInfo(unknownLine);
        }
    }
}
