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
    /// <summary>Discovers every <see cref="EosSystem"/> at world init, builds a per-phase query body for each, and drives them in topological order each frame.</summary>
    public partial class SystemsRunner : WorldBound
    {
        const float NominalDelayStep = 1f / 60f;

        sealed class SystemEntry
        {
            public readonly Action<float, ulong> Body;
            public readonly Func<bool> IsUpdate;
            public readonly Type Type;
            public readonly string Label;
            public readonly bool Reactive;
            public ulong Cursor;
            public DelayState Delay;
            public ReactiveBudgetState Budget;

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

        const ulong MaxRetentionFrames = 4096;

        sealed class DelayState
        {
            readonly bool _useFrames;
            readonly float _intervalSeconds;
            readonly int _intervalFrames;
            float _accumSeconds;
            int _accumFrames;
            public ulong LastRunFrame;

            public DelayState(float intervalSeconds)
            {
                _useFrames = false;
                _intervalSeconds = intervalSeconds <= 0f ? 0f : intervalSeconds;
                _accumSeconds = _intervalSeconds;
            }

            public DelayState(int intervalFrames)
            {
                _useFrames = true;
                _intervalFrames = intervalFrames < 1 ? 1 : intervalFrames;
                _accumFrames = _intervalFrames;
            }

            public bool Ready(float deltaTime)
            {
                if (_useFrames)
                {
                    _accumFrames++;
                    if (_accumFrames < _intervalFrames) return false;
                    _accumFrames = 0;
                    return true;
                }
                _accumSeconds += deltaTime;
                if (_accumSeconds < _intervalSeconds) return false;
                _accumSeconds -= _intervalSeconds;
                if (_accumSeconds > _intervalSeconds) _accumSeconds = 0f;
                return true;
            }

            public ulong WindowFrames(float nominalStep)
                => _useFrames
                    ? (ulong)_intervalFrames
                    : (ulong)Math.Max(1, (int)Math.Ceiling(_intervalSeconds / Math.Max(1e-6f, nominalStep)));
        }

        sealed class ReactiveBudgetState
        {
            public ulong NextCursor;
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
                    .SelectMany(assembly =>
                    {
                        try
                        {
                            return assembly.GetTypes();
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            EosLog.Error($"Assembly {assembly.GetName().Name} has invalid types, skipping partially loaded", nameof(SystemsRunner));
                            return ex.Types.Where(t => t != null);
                        }
                        catch (Exception ex)
                        {
                            EosLog.Error($"Assembly {assembly.GetName().Name} failed to load types: {ex.Message}", nameof(SystemsRunner));
                            return Enumerable.Empty<Type>();
                        }
                    })
                    .Where(t => t != null && t.IsSubclassOf(typeof(EosSystem)) && !t.IsAbstract)
                    .ToList();

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
                    var sig = SystemSignature.Of(method);
                    Action<float> body;
                    IEventChannel channel;
                    int slot;
                    try
                    {
                        var eventBinder = generated?.GetEventBody(sig);
                        if (eventBinder != null)
                        {
                            var binding = eventBinder(instance, World);
                            body = binding.Body;
                            channel = binding.Channel;
                            slot = binding.Slot;
                        }
                        else
                        {
                            if (generated != null)
                                EosLog.Warning($"{type.Name}.{method.Name}: no generated typed event body, falling back to reflection (unsupported shape or stale registry — regenerate)", nameof(SystemsRunner));
                            (body, channel, slot) = BuildEventQuery(instance, method, generated?.GetInvoker(sig));
                        }
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
                    var budgetAttr = method.GetCustomAttribute<BudgetAttribute>();
                    int budgetN = budgetAttr != null && budgetAttr.MaxPerFrame > 0 ? budgetAttr.MaxPerFrame : 0;
                    ReactiveBudgetState budgetState = null;
                    try
                    {
                        var binder = generated?.GetBody(sig);
                        bool stale = false;
                        if (binder != null)
                        {
                            var expected = generated.GetShapeHash(sig);
                            if (expected == null)
                            {
                                EosLog.Warning($"{type.Name}.{method.Name}: registry has no shape hash, regenerate to enable staleness detection", nameof(SystemsRunner));
                            }
                            else if (expected != SystemShape.ShapeHash(method))
                            {
                                EosLog.Error($"{type.Name}.{method.Name}: generated registry is stale (system shape changed since generation), falling back to reflection — regenerate", nameof(SystemsRunner));
                                stale = true;
                            }
                        }
                        if (binder != null && !stale && SystemShape.CanTypeBody(method))
                        {
                            var include = ResolveIndexedStorages(CollectIncludeTypes(method));
                            var exclude = ResolveIndexedStorages(CollectExcludeTypes(method));
                            var tagMatch = BuildTagMatch(method);
                            body = binder(instance, World, include, exclude, tagMatch);
                            reactive = SystemShape.IsReactive(method);
                        }
                        else
                        {
                            if (generated != null && !stale && budgetN == 0)
                                EosLog.Warning($"{type.Name}.{method.Name}: no generated typed body, falling back to reflection (unsupported shape or stale registry — regenerate)", nameof(SystemsRunner));
                            if (budgetN > 0 && SystemShape.IsReactive(method))
                                budgetState = new ReactiveBudgetState();
                            (body, reactive) = BuildQuery(instance, method, generated?.GetInvoker(sig), budgetN, budgetState);
                        }
                    }
                    catch (Exception ex)
                    {
                        EosLog.Error($"{type.Name}.{method.Name}: {ex.Message}", nameof(SystemsRunner));
                        continue;
                    }
                    var entry = new SystemEntry(body, isUpdate, type, type.Name, reactive, reactive ? World.Version : 0UL)
                    {
                        Delay = BuildDelay(method, type),
                        Budget = reactive ? budgetState : null
                    };

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

            World.SetReactiveRetentionFrames(ComputeRetentionFrames());

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

        DelayState BuildDelay(MethodInfo method, Type type)
        {
            var delay = method.GetCustomAttribute<DelayAttribute>();
            var delayFrame = method.GetCustomAttribute<DelayFrameAttribute>();
            if (delay != null && delayFrame != null)
            {
                EosLog.Warning($"{type.Name}.{method.Name}: both [Delay] and [DelayFrame] present, using [DelayFrame]", nameof(SystemsRunner));
                return new DelayState(delayFrame.Frames);
            }
            if (delayFrame != null) return new DelayState(delayFrame.Frames);
            if (delay != null) return new DelayState(delay.Seconds);
            return null;
        }

        ulong ComputeRetentionFrames()
        {
            ulong retention = EventsContainer.MaxAge;
            void Scan(List<SystemEntry> systems)
            {
                for (int i = 0; i < systems.Count; i++)
                {
                    var delay = systems[i].Delay;
                    if (delay == null) continue;
                    ulong window = delay.WindowFrames(NominalDelayStep);
                    if (window > retention) retention = window;
                }
            }
            Scan(_update);
            Scan(_fixedUpdate);
            Scan(_lateUpdate);
            return retention;
        }

        void Run(List<SystemEntry> systems, float deltaTime)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                var entry = systems[i];
                ulong now = World.Version;
                bool slept = false;
                bool ran = false;
                try
                {
                    if (entry.IsUpdate())
                    {
                        if (entry.Delay != null && !entry.Delay.Ready(deltaTime))
                        {
                            slept = true;
                        }
                        else
                        {
                            ran = true;
                            if (entry.Delay != null)
                            {
                                ulong gap = World.Frame - entry.Delay.LastRunFrame;
                                entry.Delay.LastRunFrame = World.Frame;
                                if (gap > World.ReactiveRetentionFrames && gap <= MaxRetentionFrames)
                                    World.SetReactiveRetentionFrames(gap);
                            }
                            if (entry.Budget != null) entry.Budget.NextCursor = now;
                            using (EosProfiler.Sample(entry.Label))
                                entry.Body(deltaTime, entry.Reactive ? entry.Cursor : 0UL);
                        }
                    }
                }
                catch (Exception ex)
                {
                    EosLog.Error($"{entry.Type.Name}.Execute threw: {ex.InnerException?.Message ?? ex.Message}", nameof(SystemsRunner));
                }
                finally
                {
                    if (entry.Reactive && !slept)
                        entry.Cursor = entry.Budget != null && ran ? entry.Budget.NextCursor : now;
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

        /// <summary>Returns the live instance of system type <typeparamref name="T"/>, or null if no such system was discovered.</summary>
        public T GetSystem<T>() where T : EosSystem => _typeToSystem.TryGetValue(typeof(T), out var system) ? (T)system : null;
        /// <summary>Every discovered system instance, in discovery order.</summary>
        public IEnumerable<EosSystem> All => _all;
        
        internal void Dispose()
        {
            _all.Clear();
            _update.Clear();
            _fixedUpdate.Clear();
            _lateUpdate.Clear();
            _eventUpdate.Clear();
            _eventFixedUpdate.Clear();
            _eventLateUpdate.Clear();
            _typeToSystem.Clear();
        }
    }
}