using System;

namespace EOS.Attributes
{
    /// <summary>Assigns an <see cref="EOS.Systems.EosSystem"/> to a <see cref="EOS.Systems.SystemGroup"/> for hierarchical enable/disable and ordering.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GroupAttribute : Attribute
    {
        /// <summary>The group type this system belongs to.</summary>
        public readonly Type Group;
        /// <summary>Assigns the system to <paramref name="group"/>.</summary>
        public GroupAttribute(Type group) => Group = group;
    }
}