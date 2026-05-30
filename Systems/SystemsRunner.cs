// Systems/SystemsRunner.cs

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
        static readonly List<(Action action, Func<bool> isUpdate, Type type)> _update = new();
        static readonly List<(Action action, Func<bool> isUpdate, Type type)> _fixedUpdate = new();
        static readonly List<(Action action, Func<bool> isUpdate, Type type)> _lateUpdate = new();
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

                var method = type.GetMethod(
                    EXECUTE_METHOD,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new Exception($"{type.Name} must have an Execute method");

                var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                var action = BuildQuery(instance, method, paramTypes);
                var groupAttr = type.GetCustomAttribute<GroupAttribute>();
                
                Func<bool> isUpdate = groupAttr != null
                    ? () => instance.IsUpdate() && SystemGroups.IsEnabled(groupAttr.Group)
                    : instance.IsUpdate;
                if (groupAttr != null)
                    SystemGroups.Register(groupAttr.Group);

                switch (instance.UpdateType)
                {
                    case UpdateType.Update: _update.Add((action, isUpdate, type)); break;
                    case UpdateType.FixedUpdate: _fixedUpdate.Add((action, isUpdate, type)); break;
                    case UpdateType.LateUpdate: _lateUpdate.Add((action, isUpdate, type)); break;
                }
            }

            TopologicalSort(_update);
            TopologicalSort(_fixedUpdate);
            TopologicalSort(_lateUpdate);

            foreach (var system in _all)
                system.Start();
        }

        public static void Update() => Run(_update);
        public static void FixedUpdate() => Run(_fixedUpdate);
        public static void LateUpdate() => Run(_lateUpdate);

        static void Run(List<(Action action, Func<bool> isUpdate, Type type)> systems)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                var s = systems[i];
                if (s.isUpdate()) s.action();
            }
        }

        static void TopologicalSort(List<(Action action, Func<bool> isUpdate, Type type)> systems)
        {
            int n = systems.Count;

            var typeToIdx = new Dictionary<Type, int>(n);
            for (int i = 0; i < n; i++)
                typeToIdx[systems[i].type] = i;

            var adj = new List<int>[n];
            var inDegree = new int[n];
            for (int i = 0; i < n; i++)
                adj[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                var type = systems[i].type;

                foreach (var attr in type.GetCustomAttributes<UpdateAfterAttribute>())
                    if (typeToIdx.TryGetValue(attr.Target, out int j))
                    {
                        adj[j].Add(i);
                        inDegree[i]++;
                    }

                foreach (var attr in type.GetCustomAttributes<UpdateBeforeAttribute>())
                    if (typeToIdx.TryGetValue(attr.Target, out int j))
                    {
                        adj[i].Add(j);
                        inDegree[j]++;
                    }
            }

            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
                if (inDegree[i] == 0) queue.Enqueue(i);

            var result = new List<(Action, Func<bool>, Type)>(n);
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