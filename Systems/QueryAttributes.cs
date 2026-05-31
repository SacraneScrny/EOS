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

    internal enum Channel : byte { None, New, Bumped }
}