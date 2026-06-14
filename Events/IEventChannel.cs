namespace EOS.Events
{
    /// <summary>Non-generic view of an <see cref="EventChannel{T}"/> used by the system runner to drain events without knowing the struct type.</summary>
    public interface IEventChannel
    {
        /// <summary>Highest sequence number assigned to a live event.</summary>
        ulong MaxSeq { get; }

        /// <summary>Number of live events currently readable.</summary>
        int LiveCount { get; }
        /// <summary>Boxed live event value at the given index.</summary>
        object BoxedAt(int index);
        /// <summary>Sequence number of the live event at the given index.</summary>
        ulong SeqAt(int index);

        /// <summary>Registers a consumer and returns its cursor slot.</summary>
        int RegisterConsumer();
        /// <summary>Current cursor watermark for the given consumer slot.</summary>
        ulong CursorOf(int slot);
        /// <summary>Marks all current live events as read for the given consumer slot.</summary>
        void Advance(int slot);

        /// <summary>Moves staged events into the live ring, stamping the given frame.</summary>
        void Promote(ulong frame);
        /// <summary>Retires fully-consumed or over-age live events.</summary>
        void Trim(ulong frame, ulong maxAge);
        /// <summary>Empties all buffers and resets sequences and cursors.</summary>
        void Clear();
    }
}
