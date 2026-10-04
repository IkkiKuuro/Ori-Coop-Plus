namespace OriCoopBepInEx.Domain
{
    public interface IPlayerStateSink
    {
        void Publish(PlayerSnapshot snapshot);
    }
}
