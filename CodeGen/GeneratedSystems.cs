using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Events;
using EOS.Storage;
using EOS.Systems;

namespace EOS.CodeGen
{
    /// <summary>Generated reflection-style invoker that calls a system method with a boxed argument array.</summary>
    public delegate void SystemInvoker(EosSystem system, object[] args);

    /// <summary>Generated factory that binds a system's <c>Execute</c> body against its storages and filters, returning a per-frame <c>(deltaTime, cursor)</c> runner.</summary>
    public delegate Action<float, ulong> SystemBodyBinder(
        EosSystem system,
        World world,
        IIndexedStorage[] include,
        IIndexedStorage[] exclude,
        Func<EosEntity, bool> tagMatch);

    /// <summary>A bound <c>EventExecute</c> body together with its event channel and consumer slot.</summary>
    public readonly struct EventBinding
    {
        /// <summary>Per-frame body that drains unread events from the channel.</summary>
        public readonly Action<float> Body;
        /// <summary>The event channel this binding consumes.</summary>
        public readonly IEventChannel Channel;
        /// <summary>The consumer slot identifying this binding's read cursor on the channel.</summary>
        public readonly int Slot;

        /// <summary>Constructs an event binding from its body, channel and consumer slot.</summary>
        public EventBinding(Action<float> body, IEventChannel channel, int slot)
        {
            Body = body;
            Channel = channel;
            Slot = slot;
        }
    }

    public delegate EventBinding EventBodyBinder(EosSystem system, World world);

    public sealed class GeneratedSystem
    {
        static readonly Dictionary<string, SystemInvoker> EmptyInvokers = new();
        static readonly Dictionary<string, SystemBodyBinder> EmptyBodies = new();
        static readonly Dictionary<string, EventBodyBinder> EmptyEventBodies = new();
        static readonly Dictionary<string, string> EmptyShapeHashes = new();

        public Type SystemType { get; }

        readonly Func<EosSystem> _factory;
        readonly Dictionary<string, SystemInvoker> _invokers;
        readonly Dictionary<string, SystemBodyBinder> _bodies;
        readonly Dictionary<string, EventBodyBinder> _eventBodies;
        readonly Dictionary<string, string> _shapeHashes;

        public GeneratedSystem(
            Type systemType,
            Func<EosSystem> factory,
            Dictionary<string, SystemInvoker> invokers,
            Dictionary<string, SystemBodyBinder> bodies,
            Dictionary<string, EventBodyBinder> eventBodies,
            Dictionary<string, string> shapeHashes = null)
        {
            SystemType = systemType;
            _factory = factory;
            _invokers = invokers ?? EmptyInvokers;
            _bodies = bodies ?? EmptyBodies;
            _eventBodies = eventBodies ?? EmptyEventBodies;
            _shapeHashes = shapeHashes ?? EmptyShapeHashes;
        }

        public EosSystem Create() => _factory();

        public SystemInvoker GetInvoker(string signature)
            => _invokers.TryGetValue(signature, out var invoker) ? invoker : null;

        public SystemBodyBinder GetBody(string signature)
            => _bodies.TryGetValue(signature, out var body) ? body : null;

        public EventBodyBinder GetEventBody(string signature)
            => _eventBodies.TryGetValue(signature, out var body) ? body : null;

        public string GetShapeHash(string signature)
            => _shapeHashes.TryGetValue(signature, out var hash) ? hash : null;
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
