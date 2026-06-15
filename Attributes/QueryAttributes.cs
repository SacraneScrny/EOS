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

    /// <summary>Marks a query parameter reactive: fires only when the component transitioned disabled→enabled (not on first ready, which is <c>[New]</c>). Pass <c>includeCascade: true</c> to also fire when an entity/parent re-activation enabled it.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class EnabledAttribute : Attribute
    {
        /// <summary>When true the channel also fires for cascade enables (entity/parent re-activation), not just explicit <c>Enable()</c>.</summary>
        public readonly bool IncludeCascade;
        /// <summary>Marks the parameter as the <c>[Enabled]</c> reactive channel; <paramref name="includeCascade"/> opts into cascade enables.</summary>
        public EnabledAttribute(bool includeCascade = false) => IncludeCascade = includeCascade;
    }

    /// <summary>Marks a query parameter reactive: fires only when the component transitioned enabled→disabled. The (still-present, now not-ready) instance is delivered. Pass <c>includeCascade: true</c> to also fire when an entity/parent deactivation disabled it.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class DisabledAttribute : Attribute
    {
        /// <summary>When true the channel also fires for cascade disables (entity/parent deactivation), not just explicit <c>Disable()</c>.</summary>
        public readonly bool IncludeCascade;
        /// <summary>Marks the parameter as the <c>[Disabled]</c> reactive channel; <paramref name="includeCascade"/> opts into cascade disables.</summary>
        public DisabledAttribute(bool includeCascade = false) => IncludeCascade = includeCascade;
    }

    /// <summary>Marks a query parameter reactive: fires once when a component of the parameter type was removed. The instance is gone, so only the owning <see cref="EOS.Entities.EosEntity"/> is delivered (the typed parameter is null). Must be the sole reactive driver; pass <c>includeCascade: true</c> to also fire when an entity destroy removed it.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class RemovedAttribute : Attribute
    {
        /// <summary>When true the channel also fires for cascade removals (entity destroy), not just explicit <c>Remove&lt;T&gt;()</c>.</summary>
        public readonly bool IncludeCascade;
        /// <summary>Marks the parameter as the <c>[Removed]</c> reactive channel; <paramref name="includeCascade"/> opts into cascade removals.</summary>
        public RemovedAttribute(bool includeCascade = false) => IncludeCascade = includeCascade;
    }

    /// <summary>Marks a query parameter optional: the component may be absent, in which case the argument is null.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class OptionalAttribute : Attribute { }

    /// <summary>On an interface parameter, fans the query out across every matching implementation rather than deduping by entity.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class EachAttribute : Attribute { }

    internal enum Channel : byte { None, New, Bumped, Enabled, Disabled, Removed }
}