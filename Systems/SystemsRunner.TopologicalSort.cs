using System;
using System.Collections.Generic;
using System.Reflection;
using EOS.Logging;
using EOS.Systems.Groups;

namespace EOS.Systems
{
    public partial class SystemsRunner
    {
        // A node in the ordering tree. It is either a leaf (all entries that share a single
        // system type) or a group (a SystemGroup type holding child nodes). UpdateAfter /
        // UpdateBefore are resolved between siblings of the same level, so groups are ordered
        // relative to each other the same way individual systems are, and the ordering applies
        // recursively to nested groups.
        sealed class SortNode
        {
            public Type Key;                                   // system type (leaf) or SystemGroup type (group)
            public bool IsGroup;
            public readonly List<SystemEntry> Entries = new(); // leaf only
            public readonly List<SortNode> Children = new();   // group only
            public HashSet<Type> Subtree;                      // every Key contained in this node's subtree
        }

        void TopologicalSort(List<SystemEntry> systems)
        {
            if (systems.Count == 0) return;

            var root = BuildTree(systems);
            ComputeSubtree(root);
            SortLevel(root.Children);

            var ordered = new List<SystemEntry>(systems.Count);
            Flatten(root.Children, ordered);

            systems.Clear();
            systems.AddRange(ordered);
        }

        SortNode BuildTree(List<SystemEntry> systems)
        {
            var root = new SortNode { IsGroup = true };
            var groupNodes = new Dictionary<Type, SortNode>();
            var leafByType = new Dictionary<Type, SortNode>();

            SortNode GetGroupNode(Type groupType)
            {
                if (groupNodes.TryGetValue(groupType, out var node)) return node;

                node = new SortNode { Key = groupType, IsGroup = true };
                groupNodes[groupType] = node;

                var parent = ParentGroupOf(groupType);
                var parentNode = parent != null ? GetGroupNode(parent) : root;
                parentNode.Children.Add(node);
                return node;
            }

            foreach (var entry in systems)
            {
                if (!leafByType.TryGetValue(entry.Type, out var leaf))
                {
                    leaf = new SortNode { Key = entry.Type, IsGroup = false };
                    leafByType[entry.Type] = leaf;

                    var groupAttr = entry.Type.GetCustomAttribute<GroupAttribute>();
                    var parentNode = groupAttr != null ? GetGroupNode(groupAttr.Group) : root;
                    parentNode.Children.Add(leaf);
                }
                leaf.Entries.Add(entry);
            }

            return root;
        }

        // Mirrors SystemGroups.Register: a group nests under its base type when that base is itself
        // a concrete SystemGroup (not the SystemGroup root), otherwise it sits at the top level.
        static Type ParentGroupOf(Type groupType)
        {
            var baseType = groupType.BaseType;
            if (baseType != null
                && baseType != typeof(SystemGroup)
                && typeof(SystemGroup).IsAssignableFrom(baseType))
                return baseType;
            return null;
        }

        static void ComputeSubtree(SortNode node)
        {
            var set = new HashSet<Type>();
            if (node.Key != null) set.Add(node.Key);
            foreach (var child in node.Children)
            {
                ComputeSubtree(child);
                set.UnionWith(child.Subtree);
            }
            node.Subtree = set;
        }

        void SortLevel(List<SortNode> nodes)
        {
            OrderSiblings(nodes);
            foreach (var node in nodes)
                if (node.IsGroup)
                    SortLevel(node.Children);
        }

        // Topological sort of a single level of siblings using UpdateAfter / UpdateBefore.
        // A target type matches the sibling whose subtree contains it, so a constraint that points
        // at a system buried inside a sibling group orders the whole sibling group. Ties are broken
        // deterministically by type name so siblings without explicit ordering no longer run in a
        // random (reflection-dependent) order.
        void OrderSiblings(List<SortNode> nodes)
        {
            int n = nodes.Count;
            if (n <= 1) return;

            var typeToSibling = new Dictionary<Type, int>();
            for (int i = 0; i < n; i++)
                foreach (var type in nodes[i].Subtree)
                    typeToSibling[type] = i; // sibling subtrees are disjoint, so no conflict

            var adj = new List<int>[n];
            var inDegree = new int[n];
            for (int i = 0; i < n; i++)
                adj[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                var key = nodes[i].Key;

                foreach (var attr in key.GetCustomAttributes<UpdateAfterAttribute>())
                    if (typeToSibling.TryGetValue(attr.Target, out int j) && j != i)
                    {
                        adj[j].Add(i);
                        inDegree[i]++;
                    }

                foreach (var attr in key.GetCustomAttributes<UpdateBeforeAttribute>())
                    if (typeToSibling.TryGetValue(attr.Target, out int j) && j != i)
                    {
                        adj[i].Add(j);
                        inDegree[j]++;
                    }
            }

            var names = new string[n];
            for (int i = 0; i < n; i++)
                names[i] = nodes[i].Key.FullName ?? nodes[i].Key.Name;

            var ready = new SortedSet<int>(Comparer<int>.Create((a, b) =>
            {
                int c = string.CompareOrdinal(names[a], names[b]);
                return c != 0 ? c : a.CompareTo(b);
            }));

            for (int i = 0; i < n; i++)
                if (inDegree[i] == 0) ready.Add(i);

            var result = new List<SortNode>(n);
            while (ready.Count > 0)
            {
                int cur = ready.Min;
                ready.Remove(cur);
                result.Add(nodes[cur]);
                foreach (int next in adj[cur])
                    if (--inDegree[next] == 0) ready.Add(next);
            }

            if (result.Count != n)
            {
                EosLog.Error("Cycle detected in EosSystem update order (systems or groups)", nameof(SystemsRunner));
                throw new Exception("Cycle detected in EosSystem update order (systems or groups)");
            }

            nodes.Clear();
            nodes.AddRange(result);
        }

        static void Flatten(List<SortNode> nodes, List<SystemEntry> output)
        {
            foreach (var node in nodes)
            {
                if (node.IsGroup)
                    Flatten(node.Children, output);
                else
                    output.AddRange(node.Entries);
            }
        }
    }
}
