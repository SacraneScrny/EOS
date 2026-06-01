using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Logging;

namespace EOS.Tags
{
    public sealed class TagsContainer : WorldBound
    {
        readonly TagRegistry _registry = new();
        readonly List<int> _scratch = new();

        ulong[] _bits = new ulong[1024];
        int _entityCapacity = 1024;
        int _words = 1;

        public TagRegistry Registry => _registry;

        public void Add(EosEntity entity, params object[] tags)
        {
            int id = entity.Id;
            if (id < 0 || tags == null) return;

            _scratch.Clear();
            for (int i = 0; i < tags.Length; i++) ResolveInto(tags[i], _scratch);
            if (_scratch.Count == 0) return;

            EnsureEntity(id);
            int b = id * _words;
            for (int i = 0; i < _scratch.Count; i++)
            {
                int bit = _scratch[i];
                _bits[b + (bit >> 6)] |= 1UL << (bit & 63);
            }
        }

        public void Remove(EosEntity entity, params object[] tags)
        {
            int id = entity.Id;
            if (id < 0 || id >= _entityCapacity || tags == null) return;

            _scratch.Clear();
            for (int i = 0; i < tags.Length; i++) ResolveInto(tags[i], _scratch);

            int b = id * _words;
            for (int i = 0; i < _scratch.Count; i++)
            {
                int bit = _scratch[i];
                _bits[b + (bit >> 6)] &= ~(1UL << (bit & 63));
            }
        }

        public bool Has(EosEntity entity, object tag)
        {
            _scratch.Clear();
            ResolveInto(tag, _scratch);
            var mask = MaskFromScratch();
            return mask != null && MatchAll(entity, mask);
        }

        public bool HasAll(EosEntity entity, params object[] tags)
            => MatchAll(entity, ScratchMask(tags));

        public bool HasAny(EosEntity entity, params object[] tags)
            => MatchAny(entity, ScratchMask(tags));

        public bool HasOne(EosEntity entity, params object[] tags)
            => MatchOne(entity, ScratchMask(tags));

        public bool HasAnyIn(EosEntity entity, Type enumType)
        {
            if (enumType == null || !enumType.IsEnum) return false;

            _scratch.Clear();
            bool flags = Attribute.IsDefined(enumType, typeof(FlagsAttribute));
            foreach (var member in Enum.GetValues(enumType))
            {
                long m = Convert.ToInt64(member);
                if (flags && m == 0) continue;
                AddBit(_registry.InternEnum(enumType, m), _scratch);
            }
            return MatchAny(entity, MaskFromScratch());
        }

        public void ClearEntity(EosEntity entity)
        {
            int id = entity.Id;
            if (id < 0 || id >= _entityCapacity) return;
            int b = id * _words;
            for (int w = 0; w < _words; w++) _bits[b + w] = 0;
        }

        public void GetTagNames(EosEntity entity, List<string> into)
        {
            if (into == null) return;
            into.Clear();
            int id = entity.Id;
            if (id < 0 || id >= _entityCapacity) return;

            int b = id * _words;
            for (int w = 0; w < _words; w++)
            {
                ulong word = _bits[b + w];
                if (word == 0) continue;
                for (int bit = 0; bit < 64; bit++)
                    if ((word & (1UL << bit)) != 0) into.Add(_registry.NameOf((w << 6) + bit));
            }
        }

        public ulong[] BuildMask(IEnumerable<object> tags)
        {
            if (tags == null) return null;
            _scratch.Clear();
            foreach (var tag in tags) ResolveInto(tag, _scratch);
            return MaskFromScratch();
        }

        public bool MatchAll(EosEntity entity, ulong[] mask)
        {
            if (mask == null) return true;
            int id = entity.Id;
            if (id < 0 || id >= _entityCapacity) return false;
            int b = id * _words;
            for (int w = 0; w < mask.Length; w++)
                if ((_bits[b + w] & mask[w]) != mask[w]) return false;
            return true;
        }

        public bool MatchNone(EosEntity entity, ulong[] mask)
        {
            if (mask == null) return true;
            int id = entity.Id;
            if (id < 0 || id >= _entityCapacity) return true;
            int b = id * _words;
            for (int w = 0; w < mask.Length; w++)
                if ((_bits[b + w] & mask[w]) != 0) return false;
            return true;
        }

        public bool MatchAny(EosEntity entity, ulong[] mask)
        {
            if (mask == null) return false;
            int id = entity.Id;
            if (id < 0 || id >= _entityCapacity) return false;
            int b = id * _words;
            for (int w = 0; w < mask.Length; w++)
                if ((_bits[b + w] & mask[w]) != 0) return true;
            return false;
        }

        public bool MatchOne(EosEntity entity, ulong[] mask)
        {
            if (mask == null) return false;
            int id = entity.Id;
            if (id < 0 || id >= _entityCapacity) return false;
            int b = id * _words;
            int count = 0;
            for (int w = 0; w < mask.Length; w++)
            {
                count += PopCount(_bits[b + w] & mask[w]);
                if (count > 1) return false;
            }
            return count == 1;
        }

        ulong[] ScratchMask(object[] tags)
        {
            _scratch.Clear();
            if (tags != null)
                for (int i = 0; i < tags.Length; i++) ResolveInto(tags[i], _scratch);
            return MaskFromScratch();
        }

        ulong[] MaskFromScratch()
        {
            if (_scratch.Count == 0) return null;

            int maxWord = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                int w = _scratch[i] >> 6;
                if (w > maxWord) maxWord = w;
            }

            var mask = new ulong[maxWord + 1];
            for (int i = 0; i < _scratch.Count; i++)
                mask[_scratch[i] >> 6] |= 1UL << (_scratch[i] & 63);
            return mask;
        }

        void ResolveInto(object tag, List<int> bits)
        {
            switch (tag)
            {
                case null:
                    return;
                case string s:
                    AddBit(_registry.InternString(s), bits);
                    return;
                case Enum e:
                    ResolveEnumInto(e, bits);
                    return;
                default:
                    EosLog.Error($"Unsupported tag '{tag}' of type {tag.GetType().Name}", nameof(TagsContainer));
                    return;
            }
        }

        void ResolveEnumInto(Enum e, List<int> bits)
        {
            var type = e.GetType();
            long value = Convert.ToInt64(e);

            if (Attribute.IsDefined(type, typeof(FlagsAttribute)))
            {
                foreach (var member in Enum.GetValues(type))
                {
                    long m = Convert.ToInt64(member);
                    if (m != 0 && (value & m) == m)
                        AddBit(_registry.InternEnum(type, m), bits);
                }
            }
            else
            {
                AddBit(_registry.InternEnum(type, value), bits);
            }
        }

        void AddBit(int index, List<int> bits)
        {
            EnsureWords((index >> 6) + 1);
            bits.Add(index);
        }

        void EnsureEntity(int id)
        {
            if (id < _entityCapacity) return;
            int cap = _entityCapacity;
            while (cap <= id) cap *= 2;
            Array.Resize(ref _bits, cap * _words);
            _entityCapacity = cap;
        }

        void EnsureWords(int words)
        {
            if (words <= _words) return;
            var old = _bits;
            var neo = new ulong[_entityCapacity * words];
            for (int id = 0; id < _entityCapacity; id++)
                Array.Copy(old, id * _words, neo, id * words, _words);
            _bits = neo;
            _words = words;
        }

        internal void Reset() => Array.Clear(_bits, 0, _bits.Length);

        static int PopCount(ulong x)
        {
            x -= (x >> 1) & 0x5555555555555555UL;
            x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
            x = (x + (x >> 4)) & 0x0f0f0f0f0f0f0f0fUL;
            return (int)((x * 0x0101010101010101UL) >> 56);
        }
    }
}
