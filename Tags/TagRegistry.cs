using System;
using System.Collections.Generic;

namespace EOS.Tags
{
    public sealed class TagRegistry
    {
        public readonly struct TagDescriptor
        {
            public readonly bool IsEnum;
            public readonly string String;
            public readonly Type EnumType;
            public readonly long EnumValue;

            public TagDescriptor(string str)
            {
                IsEnum = false;
                String = str;
                EnumType = null;
                EnumValue = 0;
            }

            public TagDescriptor(Type enumType, long enumValue)
            {
                IsEnum = true;
                String = null;
                EnumType = enumType;
                EnumValue = enumValue;
            }
        }

        readonly Dictionary<(Type type, long value), int> _enumTags = new();
        readonly Dictionary<string, int> _stringTags = new();
        readonly List<string> _names = new();
        readonly List<TagDescriptor> _descriptors = new();

        public int Count => _names.Count;

        public int InternEnum(Type type, long value)
        {
            var key = (type, value);
            if (_enumTags.TryGetValue(key, out int index)) return index;

            index = _names.Count;
            _enumTags[key] = index;
            _names.Add(type.Name + "." + Enum.ToObject(type, value));
            _descriptors.Add(new TagDescriptor(type, value));
            return index;
        }

        public int InternString(string name)
        {
            if (_stringTags.TryGetValue(name, out int index)) return index;

            index = _names.Count;
            _stringTags[name] = index;
            _names.Add(name);
            _descriptors.Add(new TagDescriptor(name));
            return index;
        }

        public string NameOf(int index)
            => index >= 0 && index < _names.Count ? _names[index] : null;

        public bool TryDescribe(int index, out TagDescriptor descriptor)
        {
            if (index >= 0 && index < _descriptors.Count)
            {
                descriptor = _descriptors[index];
                return true;
            }
            descriptor = default;
            return false;
        }
    }
}
