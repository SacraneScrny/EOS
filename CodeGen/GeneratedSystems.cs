using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Storage;
using EOS.Systems;

namespace EOS.CodeGen
{
    public delegate void SystemInvoker(EosSystem system, object[] args);

    public delegate Action<float, ulong> SystemBodyBinder(
        EosSystem system,
        World world,
        IIndexedStorage[] include,
        IIndexedStorage[] exclude,
        Func<EosEntity, bool> tagMatch);

    public sealed class GeneratedSystem
    {
        static readonly Dictionary<string, SystemInvoker> EmptyInvokers = new();
        static readonly Dictionary<string, SystemBodyBinder> EmptyBodies = new();

        public Type SystemType { get; }

        readonly Func<EosSystem> _factory;
        readonly Dictionary<string, SystemInvoker> _invokers;
        readonly Dictionary<string, SystemBodyBinder> _bodies;

        public GeneratedSystem(
            Type systemType,
            Func<EosSystem> factory,
            Dictionary<string, SystemInvoker> invokers,
            Dictionary<string, SystemBodyBinder> bodies)
        {
            SystemType = systemType;
            _factory = factory;
            _invokers = invokers ?? EmptyInvokers;
            _bodies = bodies ?? EmptyBodies;
        }

        public EosSystem Create() => _factory();

        public SystemInvoker GetInvoker(string signature)
            => _invokers.TryGetValue(signature, out var invoker) ? invoker : null;

        public SystemBodyBinder GetBody(string signature)
            => _bodies.TryGetValue(signature, out var body) ? body : null;
    }

    public static class GeneratedQuery
    {
        public static bool IncludeMatch(IIndexedStorage[] include, EosEntity entity)
        {
            for (int i = 0; i < include.Length; i++)
                if (!include[i].HasReady(entity)) return false;
            return true;
        }

        public static bool ExcludeMatch(IIndexedStorage[] exclude, EosEntity entity)
        {
            for (int i = 0; i < exclude.Length; i++)
                if (exclude[i].HasReady(entity)) return false;
            return true;
        }
    }

    public interface IGeneratedSystems
    {
        IReadOnlyList<GeneratedSystem> Systems { get; }
        void PreserveStorages(World world);
    }

    public static class GeneratedSystems
    {
        public static IGeneratedSystems Provider { get; private set; }

        public static void Register(IGeneratedSystems provider) => Provider = provider;

        public static void Clear() => Provider = null;
    }
}
