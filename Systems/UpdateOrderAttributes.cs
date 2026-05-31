using System;

namespace EOS.Systems
{
    /// <summary>
    /// Run after <paramref name="target"/>. May be placed on an EosSystem or on a SystemGroup.
    /// The target may be a system type or a SystemGroup type; ordering is resolved between
    /// siblings of the same hierarchy level and applies recursively to nested groups.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UpdateAfterAttribute : Attribute
    {
        public readonly Type Target;
        public UpdateAfterAttribute(Type target) => Target = target;
    }

    /// <summary>
    /// Run before <paramref name="target"/>. May be placed on an EosSystem or on a SystemGroup.
    /// The target may be a system type or a SystemGroup type; ordering is resolved between
    /// siblings of the same hierarchy level and applies recursively to nested groups.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UpdateBeforeAttribute : Attribute
    {
        public readonly Type Target;
        public UpdateBeforeAttribute(Type target) => Target = target;
    }
}