using System;

using EOS.Entities;
using EOS.Storage;
using EOS.Tags;

namespace EOS.Queries
{
    internal readonly struct QueryFilter
    {
        readonly TagsContainer _tags;
        readonly IIndexedStorage[] _include;
        readonly IIndexedStorage[] _exclude;
        readonly ulong[] _require;
        readonly ulong[] _forbid;
        readonly ulong[] _any;
        readonly ulong[] _one;

        QueryFilter(TagsContainer tags, IIndexedStorage[] include, IIndexedStorage[] exclude,
            ulong[] require, ulong[] forbid, ulong[] any, ulong[] one)
        {
            _tags = tags;
            _include = include;
            _exclude = exclude;
            _require = require;
            _forbid = forbid;
            _any = any;
            _one = one;
        }

        public QueryFilter With(IIndexedStorage storage)
            => new(_tags, Append(_include, storage), _exclude, _require, _forbid, _any, _one);

        public QueryFilter Without(IIndexedStorage storage)
            => new(_tags, _include, Append(_exclude, storage), _require, _forbid, _any, _one);

        public QueryFilter Require(TagsContainer tags, ulong[] mask)
            => new(tags, _include, _exclude, Merge(_require, mask), _forbid, _any, _one);

        public QueryFilter Forbid(TagsContainer tags, ulong[] mask)
            => new(tags, _include, _exclude, _require, Merge(_forbid, mask), _any, _one);

        public QueryFilter Any(TagsContainer tags, ulong[] mask)
            => new(tags, _include, _exclude, _require, _forbid, Merge(_any, mask), _one);

        public QueryFilter One(TagsContainer tags, ulong[] mask)
            => new(tags, _include, _exclude, _require, _forbid, _any, Merge(_one, mask));

        public bool Matches(EosEntity entity)
        {
            if (_include != null)
                for (int i = 0; i < _include.Length; i++)
                    if (!_include[i].HasReady(entity)) return false;

            if (_exclude != null)
                for (int i = 0; i < _exclude.Length; i++)
                    if (_exclude[i].HasReady(entity)) return false;

            if (_tags != null)
            {
                if (_require != null && !_tags.MatchAll(entity, _require)) return false;
                if (_forbid != null && !_tags.MatchNone(entity, _forbid)) return false;
                if (_any != null && !_tags.MatchAny(entity, _any)) return false;
                if (_one != null && !_tags.MatchOne(entity, _one)) return false;
            }

            return true;
        }

        static IIndexedStorage[] Append(IIndexedStorage[] source, IIndexedStorage value)
        {
            if (source == null) return new[] { value };
            var result = new IIndexedStorage[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[source.Length] = value;
            return result;
        }

        static ulong[] Merge(ulong[] a, ulong[] b)
        {
            if (b == null) return a;
            if (a == null) return b;
            var longer = a.Length >= b.Length ? a : b;
            var shorter = a.Length >= b.Length ? b : a;
            var result = new ulong[longer.Length];
            Array.Copy(longer, result, longer.Length);
            for (int i = 0; i < shorter.Length; i++) result[i] |= shorter[i];
            return result;
        }
    }
}
