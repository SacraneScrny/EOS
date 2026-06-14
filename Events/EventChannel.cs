using System;
using System.Collections.Generic;

namespace EOS.Events
{
    /// <summary>Per-type one-frame event buffer: staged events promote to a live ring read once per registered consumer cursor, then retire by min-cursor or age.</summary>
    public sealed class EventChannel<T> : IEventChannel where T : struct
    {
        struct Live
        {
            public T Value;
            public ulong Seq;
            public ulong Frame;
        }

        T[] _staging = new T[16];
        int _stagingCount;

        Live[] _live = new Live[16];
        int _head;
        int _count;

        ulong _seq;
        /// <summary>Highest sequence number assigned to a live event; consumers compare their cursor against it to early-out.</summary>
        public ulong MaxSeq => _seq;

        readonly List<ulong> _cursors = new();

        /// <summary>Copies an event into the staging buffer; it becomes live on the next <see cref="Promote"/>.</summary>
        public void Enqueue(in T e)
        {
            if (_stagingCount >= _staging.Length)
                Array.Resize(ref _staging, _staging.Length * 2);
            _staging[_stagingCount++] = e;
        }

        /// <summary>Number of live events currently readable.</summary>
        public int LiveCount => _count - _head;
        /// <summary>Live event value at the given index, boxed for the reflection path.</summary>
        public object BoxedAt(int index) => _live[_head + index].Value;
        /// <summary>Live event value at the given index, unboxed for the typed path.</summary>
        public T ValueAt(int index) => _live[_head + index].Value;
        /// <summary>Sequence number of the live event at the given index.</summary>
        public ulong SeqAt(int index) => _live[_head + index].Seq;

        /// <summary>Registers a consumer and returns its cursor slot; the consumer reads each event once by advancing past it.</summary>
        public int RegisterConsumer()
        {
            _cursors.Add(0);
            return _cursors.Count - 1;
        }
        /// <summary>Current cursor (last-read sequence watermark) for the given consumer slot.</summary>
        public ulong CursorOf(int slot) => _cursors[slot];
        /// <summary>Advances the consumer slot's cursor to the latest sequence, marking all current live events as read.</summary>
        public void Advance(int slot) => _cursors[slot] = _seq;

        /// <summary>Moves staged events into the live ring with ascending sequence numbers, stamping the given frame.</summary>
        public void Promote(ulong frame)
        {
            if (_stagingCount == 0) return;
            if (_head > 0) Compact();
            EnsureLive(_count + _stagingCount);
            for (int i = 0; i < _stagingCount; i++)
            {
                _live[_count].Value = _staging[i];
                _live[_count].Seq = ++_seq;
                _live[_count].Frame = frame;
                _count++;
            }
            Array.Clear(_staging, 0, _stagingCount);
            _stagingCount = 0;
        }

        /// <summary>Drops live events that every consumer has read (min-cursor) or that exceed <paramref name="maxAge"/> frames.</summary>
        public void Trim(ulong frame, ulong maxAge)
        {
            ulong minCur = _seq;
            if (_cursors.Count > 0)
            {
                minCur = ulong.MaxValue;
                for (int i = 0; i < _cursors.Count; i++)
                    if (_cursors[i] < minCur) minCur = _cursors[i];
            }

            while (_head < _count)
            {
                bool consumed = _live[_head].Seq <= minCur;
                bool aged = maxAge > 0 && frame - _live[_head].Frame >= maxAge;
                if (!consumed && !aged) break;
                _live[_head].Value = default;
                _head++;
            }

            if (_head >= _count)
            {
                _head = 0;
                _count = 0;
            }
        }

        /// <summary>Empties staging and live buffers and resets all sequence numbers and consumer cursors.</summary>
        public void Clear()
        {
            Array.Clear(_staging, 0, _stagingCount);
            _stagingCount = 0;
            Array.Clear(_live, 0, _count);
            _head = 0;
            _count = 0;
            _seq = 0;
            for (int i = 0; i < _cursors.Count; i++)
                _cursors[i] = 0;
        }

        void Compact()
        {
            int n = _count - _head;
            if (n > 0) Array.Copy(_live, _head, _live, 0, n);
            for (int i = n; i < _count; i++) _live[i].Value = default;
            _count = n;
            _head = 0;
        }

        void EnsureLive(int needed)
        {
            if (needed <= _live.Length) return;
            int n = _live.Length * 2;
            while (n < needed) n *= 2;
            Array.Resize(ref _live, n);
        }
    }
}
