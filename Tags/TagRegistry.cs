using System;
using System.Collections.Generic;

namespace EOS.Tags
{
    public sealed class TagRegistry
    {
        readonly Dictionary<(Type type, long value), int> _enumTags = new();
        readonly Dictionary<string, int> _stringTags = new();
        readonly List<string> _names = new();

        public int Count => _names.Count;

        public int InternEnum(Type type, long value)
        {
            var key = (type, value);
            if (_enumTags.TryGetValue(key, out int index)) return index;

            index = _names.Count;
            _enumTags[key] = index;
            _names.Add(type.Name + "." + Enum.ToObject(type, value));
            return index;
        }

        public int InternString(string name)
        {
            if (_stringTags.TryGetValue(name, out int index)) return index;

            index = _names.Count;
            _stringTags[name] = index;
            _names.Add(name);
            return index;
        }

        public string NameOf(int index)
            => index >= 0 && index < _names.Count ? _names[index] : null;
    }
}
