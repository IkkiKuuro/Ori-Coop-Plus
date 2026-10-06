using System;
using System.Collections.Generic;
using OriCoop;
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

        public static void PublishSpiritFlame(Vector3Data direction, Vector3Data origin, long timestampTicks)
        {
            INetworkService transport = s_transport;
            if (transport == null || !transport.IsConnected)
            {
                return;
            }
            SpiritFlameEventData data = new SpiritFlameEventData(
                (byte)PlayerEventKind.SpiritFlame, direction, origin, timestampTicks);
            try
            {
                transport.SendPlayerEvent(data);
            }
            catch (Exception ex)
            {
                OriCoopPlugin.LogWarning("[EVENT] PublishSpiritFlame falhou: " + ex.Message);
                return;
            }
            if (OriCoopPlugin.IsAnimVerbose())
            {
                OriCoopPlugin.LogInfo("[EVENT] kind=spiritflame fase=enviado");
            }
        }

        public static void DispatchLocal(int senderId, byte kind, SpiritFlameEventData data)
        {
            PlayerEventHandler handler;
            if (s_handlers.TryGetValue(kind, out handler) && handler != null)
            {
                if (OriCoopPlugin.IsAnimVerbose())
                {
                    OriCoopPlugin.LogInfo(string.Format("[EVENT] P{0} kind={1} fase=recebido",
                        senderId, kind));
                }
                try
                {
                    handler(senderId, data);
                }
                catch (Exception ex)
                {
                    OriCoopPlugin.LogWarning("[EVENT] handler kind=" + kind + " falhou: " + ex.Message);
                }
                return;
            }
            // Evento desconhecido: fail-closed (D-15) — mantem a ultima anim
            // valida, so registra o motivo quando verboso. Nunca publica.
            if (OriCoopPlugin.IsAnimVerbose())
            {
                OriCoopPlugin.LogInfo(string.Format("[EVENT] P{0} kind={1} aplicado=manteve-atual motivo=tipo-desconhecido",
                    senderId, kind));
            }
        }
    }
}
