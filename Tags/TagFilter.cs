using EOS.Entities;

namespace EOS.Tags
{
    internal readonly struct TagFilter
    {
        public static readonly TagFilter None = default;

        readonly TagsContainer _tags;
        readonly ulong[] _require;
        readonly ulong[] _exclude;
        readonly ulong[] _any;
        readonly ulong[] _one;

        public TagFilter(TagsContainer tags, ulong[] require, ulong[] exclude, ulong[] any, ulong[] one)
        {
            _tags = tags;
            _require = require;
            _exclude = exclude;
            _any = any;
            _one = one;
        }

        public bool Matches(EosEntity entity)
        {
            if (_tags == null) return true;
            if (_require != null && !_tags.MatchAll(entity, _require)) return false;
            if (_exclude != null && !_tags.MatchNone(entity, _exclude)) return false;
            if (_any != null && !_tags.MatchAny(entity, _any)) return false;
            if (_one != null && !_tags.MatchOne(entity, _one)) return false;
            return true;
        }
    }
}
