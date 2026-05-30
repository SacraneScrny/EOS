using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EOS.Core;
using EOS.Systems.Groups;

namespace EOS.Systems
{
    internal static partial class SystemsRunner
    {
        static readonly List<(Action<float> action, Func<bool> isUpdate, Type type)> _update = new();
        static readonly List<(Action<float> action, Func<bool> isUpdate, Type type)> _fixedUpdate = new();
        static readonly List<(Action<float> action, Func<bool> isUpdate, Type type)> _lateUpdate = new();
        static readonly List<EosSystem> _all = new();

        public static void Init()
        {
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
                _all.Add(instance);
                instance.Awake();

                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == EXECUTE_METHOD);

                if (!methods.Any())
                    continue;

                var groupAttr = type.GetCustomAttribute<GroupAttribute>();
                Func<bool> isUpdate = groupAttr != null
                    ? () => instance.IsUpdate() && SystemGroups.IsEnabled(groupAttr.Group)
                    : instance.IsUpdate;

                if (groupAttr != null)
                    SystemGroups.Register(groupAttr.Group);

                foreach (var method in methods)
                {
                    var action = BuildQuery(instance, method);

                    switch (instance.UpdateType)
                    {
                        case UpdateType.Update: _update.Add((action, isUpdate, type)); break;
                        case UpdateType.FixedUpdate: _fixedUpdate.Add((action, isUpdate, type)); break;
                        case UpdateType.LateUpdate: _lateUpdate.Add((action, isUpdate, type)); break;
                    }
                }
            }

            TopologicalSort(_update);
            TopologicalSort(_fixedUpdate);
            TopologicalSort(_lateUpdate);

            foreach (var system in _all)
                system.Start();
        }

        public static void Update(float deltaTime) => Run(_update, deltaTime);
        public static void FixedUpdate(float deltaTime) => Run(_fixedUpdate, deltaTime);
        public static void LateUpdate(float deltaTime) => Run(_lateUpdate, deltaTime);

        static void Run(List<(Action<float> action, Func<bool> isUpdate, Type type)> systems, float deltaTime)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                var s = systems[i];
                if (s.isUpdate()) s.action(deltaTime);
            }
        }

        static void TopologicalSort(List<(Action<float> action, Func<bool> isUpdate, Type type)> systems)
        {
            int n = systems.Count;
            var typeToIndices = new Dictionary<Type, List<int>>(n);
            for (int i = 0; i < n; i++)
            {
                if (!typeToIndices.TryGetValue(systems[i].type, out var list))
                {
                    list = new List<int>();
                    typeToIndices[systems[i].type] = list;
                }
                list.Add(i);
            }

            var adj = new List<int>[n];
            var inDegree = new int[n];
            for (int i = 0; i < n; i++)
                adj[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                var type = systems[i].type;
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

            var result = new List<(Action<float>, Func<bool>, Type)>(n);
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
    }
}