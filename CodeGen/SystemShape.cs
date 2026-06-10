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
    public readonly struct QueryParam
    {
        public readonly int Position;
        public readonly Type Type;
        public readonly bool IsConcrete;
        public readonly bool IsInterface;
        public readonly bool IsEntity;
        public readonly bool IsDelta;
        public readonly bool Optional;
        public readonly bool Reactive;
        public readonly bool Bumped;
        public readonly bool Each;
        public readonly bool Unsupported;

        public QueryParam(int position, Type type, bool isConcrete, bool isInterface, bool isEntity, bool isDelta, bool optional, bool reactive, bool bumped, bool each, bool unsupported)
        {
            Position = position;
            Type = type;
            IsConcrete = isConcrete;
            IsInterface = isInterface;
            IsEntity = isEntity;
            IsDelta = isDelta;
            Optional = optional;
            Reactive = reactive;
            Bumped = bumped;
            Each = each;
            Unsupported = unsupported;
        }
    }

    public static class SystemShape
    {
        public static List<QueryParam> Parameters(MethodInfo method)
        {
            var result = new List<QueryParam>();
            foreach (var p in method.GetParameters())
            {
                bool optional = p.GetCustomAttribute<OptionalAttribute>() != null;
                bool bumped = p.GetCustomAttribute<BumpedAttribute>() != null;
                bool reactive = bumped || p.GetCustomAttribute<NewAttribute>() != null;
                bool each = p.GetCustomAttribute<EachAttribute>() != null;

                var type = p.ParameterType;
                bool isEntity = type == typeof(EosEntity);
                bool isDelta = type == typeof(float);
                bool isInterface = !isEntity && !isDelta && type.IsInterface;
                bool isConcrete = !isEntity && !isDelta && !isInterface && typeof(EosObject).IsAssignableFrom(type);
                bool unsupported = !isEntity && !isDelta && !isInterface && !isConcrete;

                result.Add(new QueryParam(p.Position, type, isConcrete, isInterface, isEntity, isDelta, optional, reactive, bumped, each, unsupported));
            }
            return result;
        }

        public static bool IsReactive(MethodInfo method)
        {
            foreach (var p in Parameters(method))
                if (p.Reactive) return true;
            return false;
        }

        static readonly Dictionary<Type, List<Type>> _implCache = new();

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

        public static string ShapeHash(MethodInfo method)
        {
            var sb = new StringBuilder();
            sb.Append(SystemSignature.Of(method));

            foreach (var p in Parameters(method))
            {
                sb.Append('|').Append(p.Position).Append(':');
                if (p.Optional) sb.Append('O');
                if (p.Reactive) sb.Append(p.Bumped ? 'B' : 'N');
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

            ulong hash = 14695981039346656037UL;
            var text = sb.ToString();
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16");
        }

        public static bool CanTypeBody(MethodInfo method)
        {
            if (method.Name != "Execute") return false;

            bool anyMandatoryConcrete = false;
            bool anyMandatoryInterface = false;
            bool anyComponentOrInterface = false;
            bool reactive = false;
            bool reactiveDriver = false;

            foreach (var p in Parameters(method))
            {
                if (p.Unsupported) return false;
                if (p.Reactive)
                {
                    reactive = true;
                    if (!p.Optional && (p.IsConcrete || p.IsInterface)) reactiveDriver = true;
                }
                if (p.IsConcrete)
                {
                    anyComponentOrInterface = true;
                    if (!p.Optional) anyMandatoryConcrete = true;
                }
                else if (p.IsInterface)
                {
                    anyComponentOrInterface = true;
                    if (!p.Optional) anyMandatoryInterface = true;
                }
            }

            if (reactive) return reactiveDriver;
            return anyMandatoryConcrete || anyMandatoryInterface || !anyComponentOrInterface;
        }
    }
}
