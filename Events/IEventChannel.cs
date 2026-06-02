namespace EOS.Events
{
    public interface IEventChannel
    {
        ulong MaxSeq { get; }

        int LiveCount { get; }
        object BoxedAt(int index);
        ulong SeqAt(int index);

        int RegisterConsumer();
        ulong CursorOf(int slot);
        void Advance(int slot);

        void Promote(ulong frame);
        void Trim(ulong frame, ulong maxAge);
        void Clear();
    }
}
