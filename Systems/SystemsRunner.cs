using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using EOS.Attributes;
using EOS.Core;
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

        readonly List<SystemEntry> _update = new();
        readonly List<SystemEntry> _fixedUpdate = new();
        readonly List<SystemEntry> _lateUpdate = new();

        readonly List<EosSystem> _all = new();
        readonly Dictionary<Type, EosSystem> _typeToSystem = new();

        protected override void OnInited()
        {
            _typeToSystem.Clear();
            _all.Clear();
            _update.Clear();
            _fixedUpdate.Clear();
            _lateUpdate.Clear();

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(EosSystem)) && !t.IsAbstract);

            foreach (var type in types)
            {
                EosSystem instance;
                try
                {
                    instance = (EosSystem)Activator.CreateInstance(type);
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

                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == EXECUTE_METHOD);

                if (!methods.Any())
                    continue;

                var groupAttr = type.GetCustomAttribute<GroupAttribute>();
                Func<bool> isUpdate = groupAttr != null
                    ? () => instance.IsUpdate() && World.SystemGroups.IsEnabled(groupAttr.Group)
                    : instance.IsUpdate;

                if (groupAttr != null)
                    World.SystemGroups.Register(groupAttr.Group);

                foreach (var method in methods)
                {
                    Action<float, ulong> body;
                    bool reactive;
                    try
                    {
                        (body, reactive) = BuildQuery(instance, method);
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

            TopologicalSort(_update);
            TopologicalSort(_fixedUpdate);
            TopologicalSort(_lateUpdate);

            foreach (var system in _all)
            {
                try { system.Start(); }
                catch (Exception ex) { EosLog.Error($"{system.GetType().Name}.Start threw: {ex.Message}", nameof(SystemsRunner)); }
            }
        }

        internal void Update(float deltaTime) => Run(_update, deltaTime);
        internal void FixedUpdate(float deltaTime) => Run(_fixedUpdate, deltaTime);
        internal void LateUpdate(float deltaTime) => Run(_lateUpdate, deltaTime);

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