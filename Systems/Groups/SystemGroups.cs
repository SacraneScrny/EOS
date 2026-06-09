using System;
using System.Collections.Generic;

using EOS.Core;

namespace EOS.Systems.Groups
{
    public class SystemGroups : WorldBound
    {
        readonly Dictionary<Type, bool> _enabled = new();
        readonly Dictionary<Type, Type> _parents = new();

        internal void Register(Type groupType)
        {
            if (!_enabled.TryAdd(groupType, true)) return;

            var baseType = groupType.BaseType;
            if (baseType != null
                && baseType != typeof(SystemGroup)
                && typeof(SystemGroup).IsAssignableFrom(baseType))
            {
                _parents[groupType] = baseType;
                Register(baseType);
            }
        }

        public void SetEnabled(Type groupType, bool enabled) => _enabled[groupType] = enabled;
        public void Enable<T>() where T : SystemGroup => SetEnabled(typeof(T), true);
        public void Disable<T>() where T : SystemGroup => SetEnabled(typeof(T), false);

        public bool IsEnabled(Type groupType)
        {
            if (!_enabled.TryGetValue(groupType, out bool enabled)) return true;
            if (!enabled) return false;
            if (_parents.TryGetValue(groupType, out var parent)) return IsEnabled(parent);
            return true;
        }

        internal void Reset()
        {
            foreach (var type in _enabled)
                _enabled[type.Key] = true;
        }
    }
}