using System;
using System.Collections.Generic;

namespace EOS.Events
{
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
        public ulong MaxSeq => _seq;

        readonly List<ulong> _cursors = new();

        public void Enqueue(in T e)
        {
            if (_stagingCount >= _staging.Length)
                Array.Resize(ref _staging, _staging.Length * 2);
            _staging[_stagingCount++] = e;
        }

        public int LiveCount => _count - _head;
        public object BoxedAt(int index) => _live[_head + index].Value;
        public T ValueAt(int index) => _live[_head + index].Value;
        public ulong SeqAt(int index) => _live[_head + index].Seq;

        public int RegisterConsumer()
        {
            _cursors.Add(0);
            return _cursors.Count - 1;
        }
        public ulong CursorOf(int slot) => _cursors[slot];
        public void Advance(int slot) => _cursors[slot] = _seq;

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
