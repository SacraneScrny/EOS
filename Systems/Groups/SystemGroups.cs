using System;
using System.Collections.Generic;

namespace EOS.Systems.Groups
{
    internal static class SystemGroups
    {
        static readonly Dictionary<Type, bool> _enabled = new();
        static readonly Dictionary<Type, Type> _parents = new();

        public static void Register(Type groupType)
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

        public static void SetEnabled(Type groupType, bool enabled) => _enabled[groupType] = enabled;
        public static void Enable<T>() where T : SystemGroup => SetEnabled(typeof(T), true);
        public static void Disable<T>() where T : SystemGroup => SetEnabled(typeof(T), false);

        public static bool IsEnabled(Type groupType)
        {
            if (!_enabled.TryGetValue(groupType, out bool enabled)) return true;
            if (!enabled) return false;
            if (_parents.TryGetValue(groupType, out var parent)) return IsEnabled(parent);
            return true;
        }

        public static void Clear()
        {
            _enabled.Clear();
            _parents.Clear();
        }
    }
}