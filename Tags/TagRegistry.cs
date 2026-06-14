using System;
using System.Collections.Generic;

namespace EOS.Tags
{
    /// <summary>Interns tag keys (strings or enum type/value pairs) to stable bit indices and resolves them back to names and serializable descriptors.</summary>
    public sealed class TagRegistry
    {
        /// <summary>A serialization-stable description of one tag: either a string or an enum type plus its numeric value.</summary>
        public readonly struct TagDescriptor
        {
            /// <summary>True if this descriptor names an enum tag rather than a string tag.</summary>
            public readonly bool IsEnum;
            /// <summary>The string key, or null for enum tags.</summary>
            public readonly string String;
            /// <summary>The enum type, or null for string tags.</summary>
            public readonly Type EnumType;
            /// <summary>The numeric enum value, when <see cref="IsEnum"/> is true.</summary>
            public readonly long EnumValue;

            /// <summary>Creates a descriptor for a string tag.</summary>
            public TagDescriptor(string str)
            {
                IsEnum = false;
                String = str;
                EnumType = null;
                EnumValue = 0;
            }

            /// <summary>Creates a descriptor for an enum tag.</summary>
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

        /// <summary>The number of distinct tags interned so far.</summary>
        public int Count => _names.Count;

        /// <summary>Returns the bit index for an enum tag, assigning a new one on first use.</summary>
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

        /// <summary>Returns the bit index for a string tag, assigning a new one on first use.</summary>
        public int InternString(string name)
        {
            if (_stringTags.TryGetValue(name, out int index)) return index;

            index = _names.Count;
            _stringTags[name] = index;
            _names.Add(name);
            _descriptors.Add(new TagDescriptor(name));
            return index;
        }

        /// <summary>Returns the display name for a bit index, or null if out of range.</summary>
        public string NameOf(int index)
            => index >= 0 && index < _names.Count ? _names[index] : null;

        /// <summary>Outputs the serializable descriptor for a bit index; returns false if out of range.</summary>
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
