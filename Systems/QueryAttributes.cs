using System;

namespace EOS.Systems
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ExcludeAttribute : Attribute
    {
        public readonly Type[] Types;
        public ExcludeAttribute(params Type[] types) => Types = types;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class IncludeAttribute : Attribute
    {
        public readonly Type[] Types;
        public IncludeAttribute(params Type[] types) => Types = types;
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NewAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class BumpedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class OptionalAttribute : Attribute { }

    // Marks an interface parameter as "iterate every implementation on the entity".
    // Without it an interface parameter resolves to a single (first-found) implementation
    // and Execute runs once. With it, Execute runs once per implementation present, and
    // multiple [Each] parameters expand to the cartesian product of their implementations.
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class EachAttribute : Attribute { }

    internal enum Channel : byte { None, New, Bumped }
}