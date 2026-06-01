namespace EOS.Objects
{
    public interface IIncarnation
    {
        string Id { get; }
        void Sync();
        void SyncFixed();
        void SyncLate();
    }
}
