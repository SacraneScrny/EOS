using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Systems;

namespace EOS.CodeGen
{
    public delegate void SystemInvoker(EosSystem system, object[] args);

    public sealed class GeneratedSystem
    {
        static readonly Dictionary<string, SystemInvoker> Empty = new();

        public Type SystemType { get; }

        readonly Func<EosSystem> _factory;
        readonly Dictionary<string, SystemInvoker> _invokers;

        public GeneratedSystem(Type systemType, Func<EosSystem> factory, Dictionary<string, SystemInvoker> invokers)
        {
            SystemType = systemType;
            _factory = factory;
            _invokers = invokers ?? Empty;
        }

        public EosSystem Create() => _factory();

        public SystemInvoker GetInvoker(string signature)
            => _invokers.TryGetValue(signature, out var invoker) ? invoker : null;
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
