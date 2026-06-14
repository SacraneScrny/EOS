using System;

namespace EOS.Attributes
{
    /// <summary>Ordering edge: this system runs after <see cref="Target"/> within the same group level.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UpdateAfterAttribute : Attribute
    {
        /// <summary>The system type this one must run after.</summary>
        public readonly Type Target;
        /// <summary>Declares this system runs after <paramref name="target"/>.</summary>
        public UpdateAfterAttribute(Type target) => Target = target;
    }

    /// <summary>Ordering edge: this system runs before <see cref="Target"/> within the same group level.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UpdateBeforeAttribute : Attribute
    {
        /// <summary>The system type this one must run before.</summary>
        public readonly Type Target;
        /// <summary>Declares this system runs before <paramref name="target"/>.</summary>
        public UpdateBeforeAttribute(Type target) => Target = target;
    }

    /// <summary>Extreme tie-break priorities for <see cref="UpdateOrderAttribute"/>, mapping to <c>int.MinValue</c>/<c>int.MaxValue</c>.</summary>
    public enum UpdateOrderPhase
    {
        /// <summary>Run as early as possible (<c>int.MinValue</c>).</summary>
        BeforeAll = int.MinValue,
        /// <summary>Run as late as possible (<c>int.MaxValue</c>).</summary>
        AfterAll = int.MaxValue
    }

    /// <summary>Tie-break priority used after topological ordering; lower values run first.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class UpdateOrderAttribute : Attribute
    {
        /// <summary>The priority value; lower runs earlier.</summary>
        public readonly int Order;
        /// <summary>Sets the tie-break priority to <paramref name="order"/>.</summary>
        public UpdateOrderAttribute(int order) => Order = order;
        /// <summary>Sets the priority from an extreme <see cref="UpdateOrderPhase"/>.</summary>
        public UpdateOrderAttribute(UpdateOrderPhase phase) => Order = (int)phase;
    }
}