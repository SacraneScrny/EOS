using System;

namespace EOS.Attributes
{
    /// <summary>Base class for method-level tag filters; tags are strings or enum values.</summary>
    public abstract class TagFilterAttribute : Attribute
    {
        /// <summary>The tag keys (strings or enum values) this filter matches against.</summary>
        public readonly object[] Tags;
        /// <summary>Stores the filter's <paramref name="tags"/>.</summary>
        protected TagFilterAttribute(object[] tags) => Tags = tags;
    }

    /// <summary>Restricts a query to entities that have all the given tags.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WithTagAttribute : TagFilterAttribute
    {
        /// <summary>Requires all of <paramref name="tags"/>.</summary>
        public WithTagAttribute(params object[] tags) : base(tags) { }
    }

    /// <summary>Restricts a query to entities that have none of the given tags.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WithoutTagAttribute : TagFilterAttribute
    {
        /// <summary>Excludes entities with any of <paramref name="tags"/>.</summary>
        public WithoutTagAttribute(params object[] tags) : base(tags) { }
    }

    /// <summary>Restricts a query to entities that have at least one of the given tags.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WithAnyTagAttribute : TagFilterAttribute
    {
        /// <summary>Requires any of <paramref name="tags"/>.</summary>
        public WithAnyTagAttribute(params object[] tags) : base(tags) { }
    }

    /// <summary>Restricts a query to entities that have exactly one of the given tags.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WithOneTagAttribute : TagFilterAttribute
    {
        /// <summary>Requires exactly one of <paramref name="tags"/>.</summary>
        public WithOneTagAttribute(params object[] tags) : base(tags) { }
    }
}
