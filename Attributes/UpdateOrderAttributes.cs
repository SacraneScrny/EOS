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

    public enum UpdateOrderPhase
    {
        BeforeAll = int.MinValue,
        AfterAll = int.MaxValue
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class UpdateOrderAttribute : Attribute
    {
        public readonly int Order;
        public UpdateOrderAttribute(int order) => Order = order;
        public UpdateOrderAttribute(UpdateOrderPhase phase) => Order = (int)phase;
    }
}