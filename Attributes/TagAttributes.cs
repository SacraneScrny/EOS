using System;

namespace EOS.Systems
{
    public abstract class TagFilterAttribute : Attribute
    {
        public readonly object[] Tags;
        protected TagFilterAttribute(object[] tags) => Tags = tags;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WithTagAttribute : TagFilterAttribute
    {
        public WithTagAttribute(params object[] tags) : base(tags) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WithoutTagAttribute : TagFilterAttribute
    {
        public WithoutTagAttribute(params object[] tags) : base(tags) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WithAnyTagAttribute : TagFilterAttribute
    {
        public WithAnyTagAttribute(params object[] tags) : base(tags) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WithOneTagAttribute : TagFilterAttribute
    {
        public WithOneTagAttribute(params object[] tags) : base(tags) { }
    }
}
