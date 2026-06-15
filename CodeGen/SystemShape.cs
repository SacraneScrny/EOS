using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using EOS.Attributes;
using EOS.Entities;
using EOS.Logging;
using EOS.Objects;

namespace EOS.CodeGen
{
    /// <summary>Classified description of a single system <c>Execute</c> parameter: its type and the query role/flags derived from its type and attributes.</summary>
    public readonly struct QueryParam
    {
        /// <summary>Zero-based parameter position in the method signature.</summary>
        public readonly int Position;
        /// <summary>The declared parameter type (ref/array element unwrapped by callers).</summary>
        public readonly Type Type;
        /// <summary>True if the parameter is a concrete <see cref="EosObject"/> component.</summary>
        public readonly bool IsConcrete;
        /// <summary>True if the parameter is an interface component (fan-out across implementations).</summary>
        public readonly bool IsInterface;
        /// <summary>True if the parameter receives the owning <see cref="EosEntity"/>.</summary>
        public readonly bool IsEntity;
        /// <summary>True if the parameter receives the delta time <c>float</c>.</summary>
        public readonly bool IsDelta;
        /// <summary>True if the component is optional (<c>[Optional]</c>).</summary>
        public readonly bool Optional;
        /// <summary>True if the parameter is reactive (<c>[New]</c>/<c>[Bumped]</c>/<c>[Enabled]</c>/<c>[Disabled]</c>/<c>[Removed]</c>).</summary>
        public readonly bool Reactive;
        internal readonly Channel Channel;
        /// <summary>True if the reactive attribute opted into cascade edges (<c>includeCascade: true</c>).</summary>
        public readonly bool IncludeCascade;
        /// <summary>True if the interface parameter fans out per implementation (<c>[Each]</c>).</summary>
        public readonly bool Each;
        /// <summary>True if the parameter type cannot be classified into any query role.</summary>
        public readonly bool Unsupported;

        internal QueryParam(int position, Type type, bool isConcrete, bool isInterface, bool isEntity, bool isDelta, bool optional, bool reactive, Channel channel, bool includeCascade, bool each, bool unsupported)
        {
            Position = position;
            Type = type;
            IsConcrete = isConcrete;
            IsInterface = isInterface;
            IsEntity = isEntity;
            IsDelta = isDelta;
            Optional = optional;
            Reactive = reactive;
            Channel = channel;
            IncludeCascade = includeCascade;
            Each = each;
            Unsupported = unsupported;
        }
    }

    /// <summary>Shared shape analysis over system methods: classifies parameters, finds interface implementations, and decides which methods can take a typed (codegen) body.</summary>
    public static class SystemShape
    {
        /// <summary>Classifies every parameter of <paramref name="method"/> into <see cref="QueryParam"/> descriptors.</summary>
        public static List<QueryParam> Parameters(MethodInfo method)
        {
            var result = new List<QueryParam>();
            foreach (var p in method.GetParameters())
            {
                bool optional = p.GetCustomAttribute<OptionalAttribute>() != null;
                var channel = ChannelOf(p, out bool includeCascade);
                bool reactive = channel != Channel.None;
                bool each = p.GetCustomAttribute<EachAttribute>() != null;

                var type = p.ParameterType;
                bool isEntity = type == typeof(EosEntity);
                bool isDelta = type == typeof(float);
                bool isInterface = !isEntity && !isDelta && type.IsInterface;
                bool isConcrete = !isEntity && !isDelta && !isInterface && typeof(EosObject).IsAssignableFrom(type);
                bool unsupported = !isEntity && !isDelta && !isInterface && !isConcrete;

                result.Add(new QueryParam(p.Position, type, isConcrete, isInterface, isEntity, isDelta, optional, reactive, channel, includeCascade, each, unsupported));
            }
            return result;
        }

        /// <summary>True if any parameter of <paramref name="method"/> is a reactive channel (<c>[New]</c>/<c>[Bumped]</c>/<c>[Enabled]</c>/<c>[Disabled]</c>/<c>[Removed]</c>).</summary>
        public static bool IsReactive(MethodInfo method)
        {
            foreach (var p in Parameters(method))
                if (p.Reactive) return true;
            return false;
        }

        internal static Channel ChannelOf(ParameterInfo p, out bool includeCascade)
        {
            includeCascade = false;
            if (p.GetCustomAttribute<NewAttribute>() != null) return Channel.New;
            if (p.GetCustomAttribute<BumpedAttribute>() != null) return Channel.Bumped;
            var enabled = p.GetCustomAttribute<EnabledAttribute>();
            if (enabled != null) { includeCascade = enabled.IncludeCascade; return Channel.Enabled; }
            var disabled = p.GetCustomAttribute<DisabledAttribute>();
            if (disabled != null) { includeCascade = disabled.IncludeCascade; return Channel.Disabled; }
            var removed = p.GetCustomAttribute<RemovedAttribute>();
            if (removed != null) { includeCascade = removed.IncludeCascade; return Channel.Removed; }
            return Channel.None;
        }

        static readonly Dictionary<Type, List<Type>> _implCache = new();

        /// <summary>Returns all non-abstract, parameterless-constructible <see cref="EosObject"/> types implementing <paramref name="iface"/>, sorted and cached.</summary>
        public static IReadOnlyList<Type> ImplementationsOf(Type iface)
        {
            if (_implCache.TryGetValue(iface, out var cached)) return cached;

            var result = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                catch (Exception ex)
                {
                    EosLog.Warning($"Assembly {assembly.GetName().Name} failed to load types: {ex.Message}", nameof(SystemShape));
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null) continue;
                    if (!type.IsClass || type.IsAbstract) continue;
                    if (type.IsGenericTypeDefinition) continue;
                    if (!typeof(EosObject).IsAssignableFrom(type)) continue;
                    if (!iface.IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                    result.Add(type);
                }
            }
            result.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            _implCache[iface] = result;
            return result;
        }

        /// <summary>Computes a stable hash over the method signature and its resolved interface implementations, used to detect a stale generated registry.</summary>
        public static string ShapeHash(MethodInfo method)
        {
            var sb = new StringBuilder();
            sb.Append(SystemSignature.Of(method));

            foreach (var p in Parameters(method))
            {
                sb.Append('|').Append(p.Position).Append(':');
                if (p.Optional) sb.Append('O');
                if (p.Reactive)
                {
                    sb.Append(ChannelLetter(p.Channel));
                    if (p.IncludeCascade) sb.Append('c');
                }
                if (p.Each) sb.Append('E');
                if (p.IsInterface)
                {
                    sb.Append('[');
                    var impls = ImplementationsOf(p.Type);
                    for (int i = 0; i < impls.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append(impls[i].FullName ?? impls[i].Name);
                    }
                    sb.Append(']');
                }
            }

            return FnvHash(sb.ToString());
        }

        static char ChannelLetter(Channel channel) => channel switch
        {
            Channel.New => 'N',
            Channel.Bumped => 'B',
            Channel.Enabled => 'E',
            Channel.Disabled => 'D',
            Channel.Removed => 'R',
            _ => '?'
        };

        static string FnvHash(string text)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16");
        }

        /// <summary>True if <paramref name="method"/> is an <c>Execute</c> whose parameter shape can be emitted as a typed, allocation-free codegen body.</summary>
        public static bool CanTypeBody(MethodInfo method)
        {
            if (method.Name != "Execute") return false;

            bool anyMandatoryConcrete = false;
            bool anyMandatoryInterface = false;
            bool anyComponentOrInterface = false;
            bool reactive = false;
            bool reactiveDriver = false;
            int removedCount = 0;
            int componentOrInterfaceCount = 0;

            foreach (var p in Parameters(method))
            {
                if (p.Unsupported) return false;
                if (p.Channel == Channel.Removed && (p.IsInterface || p.Optional)) return false;
                if (p.Channel == Channel.Removed) removedCount++;
                if (p.Reactive)
                {
                    reactive = true;
                    if (!p.Optional && (p.IsConcrete || p.IsInterface)) reactiveDriver = true;
                }
                if (p.IsConcrete)
                {
                    componentOrInterfaceCount++;
                    anyComponentOrInterface = true;
                    if (!p.Optional) anyMandatoryConcrete = true;
                }
                else if (p.IsInterface)
                {
                    componentOrInterfaceCount++;
                    anyComponentOrInterface = true;
                    if (!p.Optional) anyMandatoryInterface = true;
                }
            }

            if (removedCount > 0)
                return removedCount == 1 && componentOrInterfaceCount == 1;

            if (reactive) return reactiveDriver;
            return anyMandatoryConcrete || anyMandatoryInterface || !anyComponentOrInterface;
        }
    }
}
