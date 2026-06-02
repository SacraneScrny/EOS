using System;
using System.Collections.Generic;
using System.Reflection;

using EOS.Attributes;
using EOS.Entities;
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
        public readonly bool Unsupported;

        public QueryParam(int position, Type type, bool isConcrete, bool isInterface, bool isEntity, bool isDelta, bool optional, bool reactive, bool unsupported)
        {
            Position = position;
            Type = type;
            IsConcrete = isConcrete;
            IsInterface = isInterface;
            IsEntity = isEntity;
            IsDelta = isDelta;
            Optional = optional;
            Reactive = reactive;
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
                bool reactive = p.GetCustomAttribute<NewAttribute>() != null || p.GetCustomAttribute<BumpedAttribute>() != null;

                var type = p.ParameterType;
                bool isEntity = type == typeof(EosEntity);
                bool isDelta = type == typeof(float);
                bool isInterface = !isEntity && !isDelta && type.IsInterface;
                bool isConcrete = !isEntity && !isDelta && !isInterface && typeof(EosObject).IsAssignableFrom(type);
                bool unsupported = !isEntity && !isDelta && !isInterface && !isConcrete;

                result.Add(new QueryParam(p.Position, type, isConcrete, isInterface, isEntity, isDelta, optional, reactive, unsupported));
            }
            return result;
        }

        public static bool IsReactive(MethodInfo method)
        {
            foreach (var p in Parameters(method))
                if (p.Reactive) return true;
            return false;
        }

        public static bool CanTypeBody(MethodInfo method)
        {
            if (method.Name != "Execute") return false;

            bool anyMandatoryConcrete = false;
            bool anyMandatoryInterface = false;
            bool anyComponentOrInterface = false;

            foreach (var p in Parameters(method))
            {
                if (p.Unsupported) return false;
                if (p.Reactive) return false;
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

            return anyMandatoryConcrete || anyMandatoryInterface || !anyComponentOrInterface;
        }
    }
}
