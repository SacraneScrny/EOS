using System.Collections.Generic;

using EOS.Entities;
using EOS.Hierarchy;

namespace EOS.Extensions
{
    /// <summary>Entity-facing parent-child hierarchy operations over <c>World.Hierarchy</c>; reparenting is allowed mid-iteration and emits <see cref="EOS.Hierarchy.ParentChanged"/>.</summary>
    public static class HierarchyExtensions
    {
        /// <summary>Reparents the child under <paramref name="parent"/> (pass <see cref="EosEntity.Null"/> to detach); returns false on cycle, cross-world or stale handles.</summary>
        public static bool SetParent(this EosEntity child, EosEntity parent)
            => child.World != null && child.World.Hierarchy.SetParent(child, parent);

        /// <summary>Detaches the child from its parent, making it a root entity.</summary>
        public static bool Detach(this EosEntity child)
            => child.World != null && child.World.Hierarchy.SetParent(child, EosEntity.Null);

        /// <summary>Returns the entity's parent, or <see cref="EosEntity.Null"/> if it is a root.</summary>
        public static EosEntity GetParent(this EosEntity entity)
            => entity.World != null ? entity.World.Hierarchy.GetParent(entity) : EosEntity.Null;

        /// <summary>True when the entity has a parent.</summary>
        public static bool HasParent(this EosEntity entity)
            => entity.World != null && entity.World.Hierarchy.HasParent(entity);

        /// <summary>Returns the topmost ancestor (the subtree root); the entity itself if it is a root. O(1) cached lookup.</summary>
        public static EosEntity GetRoot(this EosEntity entity)
            => entity.World != null ? entity.World.Hierarchy.GetRoot(entity) : EosEntity.Null;

        /// <summary>Number of direct children of the entity.</summary>
        public static int ChildCount(this EosEntity entity)
            => entity.World != null ? entity.World.Hierarchy.GetChildCount(entity) : 0;

        /// <summary>Alloc-free struct enumerator over the entity's direct children (order unspecified).</summary>
        public static HierarchyContainer.ChildList Children(this EosEntity entity)
            => entity.World != null ? entity.World.Hierarchy.ChildrenOf(entity) : default;

        /// <summary>Appends the entity's children into <paramref name="into"/> (BFS when <paramref name="recursive"/>); returns the number added.</summary>
        public static int GetChildren(this EosEntity entity, List<EosEntity> into, bool recursive = false)
            => entity.World != null ? entity.World.Hierarchy.Collect(entity, into, recursive) : 0;

        /// <summary>True when the entity is a direct child of <paramref name="parent"/>.</summary>
        public static bool IsChildOf(this EosEntity entity, EosEntity parent)
            => parent.IsValid && entity.GetParent() == parent;

        /// <summary>True when <paramref name="ancestor"/> is anywhere above the entity in the hierarchy.</summary>
        public static bool IsDescendantOf(this EosEntity entity, EosEntity ancestor)
            => entity.World != null && entity.World.Hierarchy.IsDescendantOf(entity, ancestor);

        /// <summary>Detaches all direct children of the entity, making each a root; spares them from a subsequent destroy cascade.</summary>
        public static void DetachChildren(this EosEntity entity)
            => entity.World?.Hierarchy.DetachChildren(entity);

        /// <summary>Creates a new entity in the same world and parents it under this entity; returns <see cref="EosEntity.Null"/> if the parent is invalid.</summary>
        public static EosEntity CreateChild(this EosEntity parent, string name = "", bool active = false, bool isSerializable = true)
        {
            if (parent.World == null || !parent.IsValid) return EosEntity.Null;
            var child = new EosEntity(parent.World, name, active, isSerializable);
            parent.World.Hierarchy.SetParent(child, parent);
            return child;
        }
    }
}
