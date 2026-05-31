using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EOS.Core;
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
            public readonly bool Reactive;
            public ulong Cursor;

            public SystemEntry(Action<float, ulong> body, Func<bool> isUpdate, Type type, bool reactive, ulong cursor)
            {
                Body = body;
                IsUpdate = isUpdate;
                Type = type;
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
                var instance = (EosSystem)Activator.CreateInstance(type);
                instance.SetWorld(World);
                _all.Add(instance);
                _typeToSystem[type] = instance;
                instance.Awake();

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
                    var (body, reactive) = BuildQuery(instance, method);
                    var entry = new SystemEntry(body, isUpdate, type, reactive, reactive ? World.Version : 0UL);

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
                system.Start();
        }

        internal void Update(float deltaTime) => Run(_update, deltaTime);
        internal void FixedUpdate(float deltaTime) => Run(_fixedUpdate, deltaTime);
        internal void LateUpdate(float deltaTime) => Run(_lateUpdate, deltaTime);

        void Run(List<SystemEntry> systems, float deltaTime)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                var entry = systems[i];

                if (entry.Reactive)
                {
                    ulong now = World.Version;
                    if (entry.IsUpdate())
                        entry.Body(deltaTime, entry.Cursor);
                    entry.Cursor = now;
                }
                else if (entry.IsUpdate())
                {
                    entry.Body(deltaTime, 0UL);
                }
            }
        }

        void TopologicalSort(List<SystemEntry> systems)
        {
            int n = systems.Count;
            var typeToIndices = new Dictionary<Type, List<int>>(n);
            for (int i = 0; i < n; i++)
            {
                if (!typeToIndices.TryGetValue(systems[i].Type, out var list))
                {
                    list = new List<int>();
                    typeToIndices[systems[i].Type] = list;
                }
                list.Add(i);
            }

            var adj = new List<int>[n];
            var inDegree = new int[n];
            for (int i = 0; i < n; i++)
                adj[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                var type = systems[i].Type;
                foreach (var attr in type.GetCustomAttributes<UpdateAfterAttribute>())
                    if (typeToIndices.TryGetValue(attr.Target, out var targets))
                    {
                        foreach (int j in targets)
                        {
                            adj[j].Add(i);
                            inDegree[i]++;
                        }
                    }

                foreach (var attr in type.GetCustomAttributes<UpdateBeforeAttribute>())
                    if (typeToIndices.TryGetValue(attr.Target, out var targets))
                    {
                        foreach (int j in targets)
                        {
                            adj[i].Add(j);
                            inDegree[j]++;
                        }
                    }
            }

            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
                if (inDegree[i] == 0) queue.Enqueue(i);

            var result = new List<SystemEntry>(n);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                result.Add(systems[cur]);
                foreach (int next in adj[cur])
                    if (--inDegree[next] == 0) queue.Enqueue(next);
            }

            if (result.Count != n)
                throw new Exception("Cycle detected in EosSystem update order");

            systems.Clear();
            systems.AddRange(result);
        }

        public T GetSystem<T>() where T : EosSystem => _typeToSystem.TryGetValue(typeof(T), out var system) ? (T)system : null;
        public IEnumerable<EosSystem> All => _all;
    }
}