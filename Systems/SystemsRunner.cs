using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using EOS.Attributes;
using EOS.CodeGen;
using EOS.Core;
using EOS.Events;
using EOS.Logging;
using EOS.Profiling;
using EOS.Systems.Groups;

namespace EOS.Systems
{
    public partial class SystemsRunner : WorldBound
    {
        sealed class SystemEntry
        {
            public readonly Action<float, ulong> Body;
            public readonly Func<bool> IsUpdate;
            public readonly Type Type;
            public readonly string Label;
            public readonly bool Reactive;
            public ulong Cursor;

            public SystemEntry(Action<float, ulong> body, Func<bool> isUpdate, Type type, string label, bool reactive, ulong cursor)
            {
                Body = body;
                IsUpdate = isUpdate;
                Type = type;
                Label = label;
                Reactive = reactive;
                Cursor = cursor;
            }
        }

        sealed class EventEntry
        {
            public readonly Action<float> Body;
            public readonly Func<bool> IsUpdate;
            public readonly IEventChannel Channel;
            public readonly int Slot;
            public readonly Type Type;
            public readonly string Label;

            public EventEntry(Action<float> body, Func<bool> isUpdate, IEventChannel channel, int slot, Type type, string label)
            {
                Body = body;
                IsUpdate = isUpdate;
                Channel = channel;
                Slot = slot;
                Type = type;
                Label = label;
            }
        }

        readonly List<SystemEntry> _update = new();
        readonly List<SystemEntry> _fixedUpdate = new();
        readonly List<SystemEntry> _lateUpdate = new();

        readonly List<EventEntry> _eventUpdate = new();
        readonly List<EventEntry> _eventFixedUpdate = new();
        readonly List<EventEntry> _eventLateUpdate = new();

        readonly List<EosSystem> _all = new();
        readonly Dictionary<Type, EosSystem> _typeToSystem = new();

        protected override void OnInited()
        {
            _typeToSystem.Clear();
            _all.Clear();
            _update.Clear();
            _fixedUpdate.Clear();
            _lateUpdate.Clear();
            _eventUpdate.Clear();
            _eventFixedUpdate.Clear();
            _eventLateUpdate.Clear();

            var sources = new List<(Type type, Func<EosSystem> factory, GeneratedSystem generated)>();
            var provider = GeneratedSystems.Provider;
            if (provider != null)
            {
                try { provider.PreserveStorages(World); }
                catch (Exception ex) { EosLog.Error($"PreserveStorages threw: {ex.Message}", nameof(SystemsRunner)); }

                var generated = provider.Systems;
                for (int i = 0; i < generated.Count; i++)
                {
                    var entry = generated[i];
                    sources.Add((entry.SystemType, entry.Create, entry));
                }
            }
            else
            {
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => t.IsSubclassOf(typeof(EosSystem)) && !t.IsAbstract);

                foreach (var type in types)
                {
                    var captured = type;
                    sources.Add((captured, () => (EosSystem)Activator.CreateInstance(captured), null));
                }
            }

            foreach (var (type, factory, generated) in sources)
            {
                EosSystem instance;
                try
                {
                    instance = factory();
                }
                catch (Exception ex)
                {
                    EosLog.Error($"Failed to instantiate {type.Name}: {ex.Message}", nameof(SystemsRunner));
                    continue;
                }

                instance.SetWorld(World);
                _all.Add(instance);
                _typeToSystem[type] = instance;

                try { instance.Awake(); }
                catch (Exception ex) { EosLog.Error($"{type.Name}.Awake threw: {ex.Message}", nameof(SystemsRunner)); }

                var allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var methods = allMethods.Where(m => m.Name == EXECUTE_METHOD).ToArray();
                var eventMethods = allMethods.Where(m => m.Name == EVENT_EXECUTE_METHOD).ToArray();

                if (methods.Length == 0 && eventMethods.Length == 0)
                    continue;

                var groupAttr = type.GetCustomAttribute<GroupAttribute>();
                Func<bool> isUpdate = groupAttr != null
                    ? () => instance.IsUpdate() && World.SystemGroups.IsEnabled(groupAttr.Group)
                    : instance.IsUpdate;

                if (groupAttr != null)
                    World.SystemGroups.Register(groupAttr.Group);

                foreach (var method in eventMethods)
                {
                    Action<float> body;
                    IEventChannel channel;
                    int slot;
                    try
                    {
                        (body, channel, slot) = BuildEventQuery(instance, method, generated?.GetInvoker(SystemSignature.Of(method)));
                    }
                    catch (Exception ex)
                    {
                        EosLog.Error($"{type.Name}.{method.Name}: {ex.Message}", nameof(SystemsRunner));
                        continue;
                    }
                    var eventEntry = new EventEntry(body, isUpdate, channel, slot, type, $"{type.Name}.{method.Name}");

                    switch (instance.UpdateType)
                    {
                        case UpdateType.Update: _eventUpdate.Add(eventEntry); break;
                        case UpdateType.FixedUpdate: _eventFixedUpdate.Add(eventEntry); break;
                        case UpdateType.LateUpdate: _eventLateUpdate.Add(eventEntry); break;
                    }
                }

                foreach (var method in methods)
                {
                    var sig = SystemSignature.Of(method);
                    Action<float, ulong> body;
                    bool reactive;
                    try
                    {
                        var binder = generated?.GetBody(sig);
                        if (binder != null && SystemShape.CanTypeBody(method))
                        {
                            var include = ResolveIndexedStorages(CollectIncludeTypes(method));
                            var exclude = ResolveIndexedStorages(CollectExcludeTypes(method));
                            var tagMatch = BuildTagMatch(method);
                            body = binder(instance, World, include, exclude, tagMatch);
                            reactive = false;
                        }
                        else
                        {
                            if (generated != null)
                                EosLog.Warning($"{type.Name}.{method.Name}: no generated typed body, falling back to reflection (unsupported shape or stale registry — regenerate)", nameof(SystemsRunner));
                            (body, reactive) = BuildQuery(instance, method, generated?.GetInvoker(sig));
                        }
                    }
                    catch (Exception ex)
                    {
                        EosLog.Error($"{type.Name}.{method.Name}: {ex.Message}", nameof(SystemsRunner));
                        continue;
                    }
                    var entry = new SystemEntry(body, isUpdate, type, type.Name, reactive, reactive ? World.Version : 0UL);

                    switch (instance.UpdateType)
                    {
                        case UpdateType.Update: _update.Add(entry); break;
                        case UpdateType.FixedUpdate: _fixedUpdate.Add(entry); break;
                        case UpdateType.LateUpdate: _lateUpdate.Add(entry); break;
                    }
                }
            }

            TopologicalSort(_update, e => e.Type);
            TopologicalSort(_fixedUpdate, e => e.Type);
            TopologicalSort(_lateUpdate, e => e.Type);

            TopologicalSort(_eventUpdate, e => e.Type);
            TopologicalSort(_eventFixedUpdate, e => e.Type);
            TopologicalSort(_eventLateUpdate, e => e.Type);

            foreach (var system in _all)
            {
                try { system.Start(); }
                catch (Exception ex) { EosLog.Error($"{system.GetType().Name}.Start threw: {ex.Message}", nameof(SystemsRunner)); }
            }
        }

        internal void Update(float deltaTime) => Run(_update, deltaTime);
        internal void FixedUpdate(float deltaTime) => Run(_fixedUpdate, deltaTime);
        internal void LateUpdate(float deltaTime) => Run(_lateUpdate, deltaTime);

        internal void UpdateEvents(float deltaTime) => RunEvents(_eventUpdate, deltaTime);
        internal void FixedUpdateEvents(float deltaTime) => RunEvents(_eventFixedUpdate, deltaTime);
        internal void LateUpdateEvents(float deltaTime) => RunEvents(_eventLateUpdate, deltaTime);

        void RunEvents(List<EventEntry> entries, float deltaTime)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                try
                {
                    if (entry.IsUpdate())
                        using (EosProfiler.Sample(entry.Label))
                            entry.Body(deltaTime);
                }
                catch (Exception ex)
                {
                    EosLog.Error($"{entry.Label} threw: {ex.InnerException?.Message ?? ex.Message}", nameof(SystemsRunner));
                }
                finally { entry.Channel.Advance(entry.Slot); }
            }
        }

        void Run(List<SystemEntry> systems, float deltaTime)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                var entry = systems[i];
                try
                {
                    if (entry.Reactive)
                    {
                        ulong now = World.Version;
                        if (entry.IsUpdate())
                        {
                            using (EosProfiler.Sample(entry.Label))
                                entry.Body(deltaTime, entry.Cursor);
                        }
                        entry.Cursor = now;
                    }
                    else if (entry.IsUpdate())
                    {
                        using (EosProfiler.Sample(entry.Label))
                            entry.Body(deltaTime, 0UL);
                    }
                }
                catch (Exception ex)
                {
                    EosLog.Error($"{entry.Type.Name}.Execute threw: {ex.InnerException?.Message ?? ex.Message}", nameof(SystemsRunner));
                }
            }
        }

        internal void DebugDraw()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                try { _all[i].OnDebugDraw(); }
                catch (Exception ex) { EosLog.Error($"{_all[i].GetType().Name}.OnDebugDraw threw: {ex.Message}", nameof(SystemsRunner)); }
            }
        }

        public T GetSystem<T>() where T : EosSystem => _typeToSystem.TryGetValue(typeof(T), out var system) ? (T)system : null;
        public IEnumerable<EosSystem> All => _all;
    }
}