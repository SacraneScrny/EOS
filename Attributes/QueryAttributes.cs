using System;

namespace EOS.Attributes
{
    /// <summary>Method-level filter restricting an <c>Execute</c> query to entities that do NOT have the listed component types.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ExcludeAttribute : Attribute
    {
        /// <summary>Component types an entity must not have to match.</summary>
        public readonly Type[] Types;
        /// <summary>Excludes entities carrying any of <paramref name="types"/>.</summary>
        public ExcludeAttribute(params Type[] types) => Types = types;
    }

    /// <summary>Method-level filter restricting an <c>Execute</c> query to entities that have all the listed component types.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class IncludeAttribute : Attribute
    {
        /// <summary>Component types an entity must have to match.</summary>
        public readonly Type[] Types;
        /// <summary>Requires entities to carry all of <paramref name="types"/>.</summary>
        public IncludeAttribute(params Type[] types) => Types = types;
    }

    /// <summary>Marks a query parameter reactive: fires only when the component was recently added (<c>MarkReady</c>).</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NewAttribute : Attribute { }

    /// <summary>Marks a query parameter reactive: fires only when <c>Bump()</c> was called on the component this version window.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class BumpedAttribute : Attribute { }

    /// <summary>Marks a query parameter optional: the component may be absent, in which case the argument is null.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class OptionalAttribute : Attribute { }

    /// <summary>On an interface parameter, fans the query out across every matching implementation rather than deduping by entity.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class EachAttribute : Attribute { }

    internal enum Channel : byte { None, New, Bumped }
}