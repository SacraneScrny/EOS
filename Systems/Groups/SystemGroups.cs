using System;
using System.Collections.Generic;

using EOS.Core;

namespace EOS.Systems.Groups
{
    /// <summary>Per-world registry of <see cref="SystemGroup"/> enable state with hierarchical (ancestor-aware) resolution.</summary>
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

        /// <summary>Sets the own enabled flag for a group type (does not affect ancestors).</summary>
        public void SetEnabled(Type groupType, bool enabled) => _enabled[groupType] = enabled;
        /// <summary>Enables group <typeparamref name="T"/>.</summary>
        public void Enable<T>() where T : SystemGroup => SetEnabled(typeof(T), true);
        /// <summary>Disables group <typeparamref name="T"/>, suspending its systems and descendants.</summary>
        public void Disable<T>() where T : SystemGroup => SetEnabled(typeof(T), false);

        /// <summary>True only if the group and every ancestor group is enabled; unknown groups count as enabled.</summary>
        public bool IsEnabled(Type groupType)
        {
            if (!_enabled.TryGetValue(groupType, out bool enabled)) return true;
            if (!enabled) return false;
            if (_parents.TryGetValue(groupType, out var parent)) return IsEnabled(parent);
            return true;
        }

        internal void Reset()
        {
            var keys = new List<Type>(_enabled.Keys);
            for (int i = 0; i < keys.Count; i++)
                _enabled[keys[i]] = true;
        }
    }
}