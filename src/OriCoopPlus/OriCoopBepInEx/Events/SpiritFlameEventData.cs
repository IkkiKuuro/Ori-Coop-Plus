using OriCoopBepInEx.Domain;

namespace OriCoopBepInEx.Events
{
    // DTO do evento piloto Spirit Flame (fase 3, D-11 payload minimo).
    // Campos publicos mutaveis + construtor explicito, sem tipos UnityEngine
    // (camada estilo Domain; a conversao para UnityEngine.Vector3 acontece
    // nas bordas: patch publica, puppet aplica). Ordem dos campos = ordem
    // congelada no fio (PlayerEventProtocol).
    public struct SpiritFlameEventData
    {
        public byte Kind;
        public Vector3Data Direction;
        public Vector3Data Origin;
        public long TimestampTicks;

        public SpiritFlameEventData(byte kind, Vector3Data direction, Vector3Data origin, long timestampTicks)
        {
            Kind = kind;
            Direction = direction;
            Origin = origin;
            TimestampTicks = timestampTicks;
        }
    }
}
