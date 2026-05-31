

using System;

namespace EOS.Systems
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UpdateAfterAttribute : Attribute
    {
        public readonly Type Target;
        public UpdateAfterAttribute(Type target) => Target = target;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UpdateBeforeAttribute : Attribute
    {
        public readonly Type Target;
        public UpdateBeforeAttribute(Type target) => Target = target;
    }
}