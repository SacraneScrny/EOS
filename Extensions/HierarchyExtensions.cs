using System.Collections.Generic;

using EOS.Entities;
using EOS.Hierarchy;

namespace EOS.Extensions
{
    public static class HierarchyExtensions
    {
        public static bool SetParent(this EosEntity child, EosEntity parent)
            => child.World != null && child.World.Hierarchy.SetParent(child, parent);

        public static bool Detach(this EosEntity child)
            => child.World != null && child.World.Hierarchy.SetParent(child, EosEntity.Null);

        public static EosEntity GetParent(this EosEntity entity)
            => entity.World != null ? entity.World.Hierarchy.GetParent(entity) : EosEntity.Null;

        public static bool HasParent(this EosEntity entity)
            => entity.World != null && entity.World.Hierarchy.HasParent(entity);

        public static EosEntity GetRoot(this EosEntity entity)
            => entity.World != null ? entity.World.Hierarchy.GetRoot(entity) : EosEntity.Null;

        public static int ChildCount(this EosEntity entity)
            => entity.World != null ? entity.World.Hierarchy.GetChildCount(entity) : 0;

        public static HierarchyContainer.ChildList Children(this EosEntity entity)
            => entity.World != null ? entity.World.Hierarchy.ChildrenOf(entity) : default;

        public static int GetChildren(this EosEntity entity, List<EosEntity> into, bool recursive = false)
            => entity.World != null ? entity.World.Hierarchy.Collect(entity, into, recursive) : 0;

        public static bool IsChildOf(this EosEntity entity, EosEntity parent)
            => parent.IsValid && entity.GetParent() == parent;

        public static bool IsDescendantOf(this EosEntity entity, EosEntity ancestor)
            => entity.World != null && entity.World.Hierarchy.IsDescendantOf(entity, ancestor);

        public static void DetachChildren(this EosEntity entity)
            => entity.World?.Hierarchy.DetachChildren(entity);

        public static EosEntity CreateChild(this EosEntity parent, string name = "", bool active = false, bool isSerializable = true)
        {
            if (parent.World == null || !parent.IsValid) return EosEntity.Null;
            var child = new EosEntity(parent.World, name, active, isSerializable);
            parent.World.Hierarchy.SetParent(child, parent);
            return child;
        }
    }
}
