using System;
using System.Collections.Generic;
using System.Reflection;
using EOS.Logging;

namespace EOS.Systems
{
    public partial class SystemsRunner
    {
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
                        foreach (int j in targets)
                        {
                            adj[j].Add(i);
                            inDegree[i]++;
                        }

                foreach (var attr in type.GetCustomAttributes<UpdateBeforeAttribute>())
                    if (typeToIndices.TryGetValue(attr.Target, out var targets))
                        foreach (int j in targets)
                        {
                            adj[i].Add(j);
                            inDegree[j]++;
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
            {
                EosLog.Error("Cycle detected in EosSystem update order", nameof(SystemsRunner));
                throw new Exception("Cycle detected in EosSystem update order");
            }

            systems.Clear();
            systems.AddRange(result);
        }
    }
}