using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Logging;

namespace EOS.Hierarchy
{
    public sealed class HierarchyContainer : WorldBound
    {
        int[] _parents;
        int[] _firstChild;
        int[] _nextSibling;
        int[] _prevSibling;
        int[] _childCount;
        bool[] _branchActive;
        int _capacity;

        readonly Stack<int> _walk = new();

        public HierarchyContainer()
        {
            _capacity = 1024;
            _parents = new int[_capacity];
            _firstChild = new int[_capacity];
            _nextSibling = new int[_capacity];
            _prevSibling = new int[_capacity];
            _childCount = new int[_capacity];
            _branchActive = new bool[_capacity];
            ClearSlots(0, _capacity);
        }

        public bool SetParent(EosEntity child, EosEntity parent)
        {
            if (child.World != World || !child.IsValid)
            {
                EosLog.Error("SetParent: child entity is invalid", nameof(HierarchyContainer));
                return false;
            }
            if (parent.World == null) return Detach(child);
            if (parent.World != World)
            {
                EosLog.Error("SetParent: parent belongs to another world", nameof(HierarchyContainer));
                return false;
            }
            if (!parent.IsValid)
            {
                EosLog.Error("SetParent: parent entity is invalid", nameof(HierarchyContainer));
                return false;
            }
            if (parent.Id == child.Id)
            {
                EosLog.Error($"SetParent: cannot parent '{child.Name}' to itself", nameof(HierarchyContainer));
                return false;
            }

            EnsureCapacity(child.Id > parent.Id ? child.Id : parent.Id);
            if (_parents[child.Id] == parent.Id) return true;

            if (IsDescendantOf(parent, child))
            {
                EosLog.Error($"SetParent: parenting '{child.Name}' under '{parent.Name}' would create a cycle", nameof(HierarchyContainer));
                return false;
            }

            var oldParent = ParentOf(child.Id);
            Unlink(child.Id);
            Link(parent.Id, child.Id);
            Refresh(child);
            World.Event(new ParentChanged(child, oldParent, parent));
            return true;
        }

        public EosEntity GetParent(EosEntity entity)
        {
            if (entity.World != World || !entity.IsValid) return EosEntity.Null;
            int id = entity.Id;
            return id < _capacity ? ParentOf(id) : EosEntity.Null;
        }

        public bool HasParent(EosEntity entity)
            => entity.World == World && entity.IsValid && entity.Id < _capacity && _parents[entity.Id] >= 0;

        public EosEntity GetRoot(EosEntity entity)
        {
            if (entity.World != World || !entity.IsValid) return EosEntity.Null;
            int id = entity.Id;
            if (id >= _capacity) return entity;
            while (_parents[id] >= 0) id = _parents[id];
            return World.Entities.EntityFromId(id);
        }

        public bool IsDescendantOf(EosEntity entity, EosEntity ancestor)
        {
            if (entity.World != World || ancestor.World != World) return false;
            if (!entity.IsValid || !ancestor.IsValid) return false;
            if (entity.Id >= _capacity) return false;
            int current = _parents[entity.Id];
            while (current >= 0)
            {
                if (current == ancestor.Id) return true;
                current = _parents[current];
            }
            return false;
        }

        public int GetChildCount(EosEntity entity)
            => entity.World == World && entity.IsValid && entity.Id < _capacity ? _childCount[entity.Id] : 0;

        public ChildList ChildrenOf(EosEntity entity)
            => entity.World == World && entity.IsValid && entity.Id < _capacity
                ? new ChildList(this, _firstChild[entity.Id])
                : new ChildList(this, -1);

        public int Collect(EosEntity entity, List<EosEntity> into, bool recursive = false)
        {
            if (into == null) return 0;
            if (entity.World != World || !entity.IsValid || entity.Id >= _capacity) return 0;

            int before = into.Count;
            AppendChildren(entity.Id, into);
            if (recursive)
            {
                int cursor = before;
                while (cursor < into.Count)
                {
                    AppendChildren(into[cursor].Id, into);
                    cursor++;
                }
            }
            return into.Count - before;
        }

        public void DetachChildren(EosEntity entity)
        {
            if (entity.World != World || !entity.IsValid || entity.Id >= _capacity) return;
            int id = entity.Id;
            while (_firstChild[id] >= 0)
                Detach(World.Entities.EntityFromId(_firstChild[id]));
        }

        internal bool IsBranchActive(int id)
            => id >= 0 && id < _capacity && _branchActive[id];

        internal void OnEntityCreated(int id, bool active)
        {
            EnsureCapacity(id);
            _parents[id] = -1;
            _firstChild[id] = -1;
            _nextSibling[id] = -1;
            _prevSibling[id] = -1;
            _childCount[id] = 0;
            _branchActive[id] = active;
        }

        internal void OnSelfActiveChanged(EosEntity entity)
        {
            EnsureCapacity(entity.Id);
            Refresh(entity);
        }

        internal void OnEntityDestroying(EosEntity entity)
        {
            int id = entity.Id;
            if (id < 0 || id >= _capacity) return;
            while (_firstChild[id] >= 0)
            {
                int childId = _firstChild[id];
                World.Entities.EntityFromId(childId).Destroy();
                if (_firstChild[id] == childId) Unlink(childId);
            }
            Unlink(id);
            _branchActive[id] = false;
        }

        internal void Reset()
        {
            ClearSlots(0, _capacity);
            _walk.Clear();
        }

        bool Detach(EosEntity child)
        {
            int id = child.Id;
            if (id >= _capacity || _parents[id] < 0) return true;
            var oldParent = ParentOf(id);
            Unlink(id);
            Refresh(child);
            World.Event(new ParentChanged(child, oldParent, EosEntity.Null));
            return true;
        }

        EosEntity ParentOf(int id)
        {
            int p = _parents[id];
            return p < 0 ? EosEntity.Null : World.Entities.EntityFromId(p);
        }

        void AppendChildren(int id, List<EosEntity> into)
        {
            for (int child = _firstChild[id]; child >= 0; child = _nextSibling[child])
                into.Add(World.Entities.EntityFromId(child));
        }

        void Link(int parentId, int childId)
        {
            _parents[childId] = parentId;
            int head = _firstChild[parentId];
            _nextSibling[childId] = head;
            _prevSibling[childId] = -1;
            if (head >= 0) _prevSibling[head] = childId;
            _firstChild[parentId] = childId;
            _childCount[parentId]++;
        }

        void Unlink(int childId)
        {
            int parentId = _parents[childId];
            if (parentId < 0) return;
            int prev = _prevSibling[childId];
            int next = _nextSibling[childId];
            if (prev >= 0) _nextSibling[prev] = next;
            else _firstChild[parentId] = next;
            if (next >= 0) _prevSibling[next] = prev;
            _parents[childId] = -1;
            _nextSibling[childId] = -1;
            _prevSibling[childId] = -1;
            _childCount[parentId]--;
        }

        void Refresh(EosEntity entity)
        {
            int id = entity.Id;
            bool effective = World.Entities.IsActiveSelf(entity) && ParentBranchActive(id);
            if (_branchActive[id] == effective) return;
            _branchActive[id] = effective;
            World.ObjectsStorages.RefreshReadyAll(entity);
            Propagate(id);
        }

        bool ParentBranchActive(int id)
        {
            int p = _parents[id];
            return p < 0 || _branchActive[p];
        }

        void Propagate(int id)
        {
            _walk.Clear();
            _walk.Push(id);
            while (_walk.Count > 0)
            {
                int current = _walk.Pop();
                bool parentActive = _branchActive[current];
                for (int child = _firstChild[current]; child >= 0; child = _nextSibling[child])
                {
                    var childEntity = World.Entities.EntityFromId(child);
                    bool effective = parentActive && World.Entities.IsActiveSelf(childEntity);
                    if (_branchActive[child] == effective) continue;
                    _branchActive[child] = effective;
                    World.ObjectsStorages.RefreshReadyAll(childEntity);
                    _walk.Push(child);
                }
            }
        }

        void EnsureCapacity(int id)
        {
            if (id < _capacity) return;
            int cap = _capacity;
            while (cap <= id) cap *= 2;
            Array.Resize(ref _parents, cap);
            Array.Resize(ref _firstChild, cap);
            Array.Resize(ref _nextSibling, cap);
            Array.Resize(ref _prevSibling, cap);
            Array.Resize(ref _childCount, cap);
            Array.Resize(ref _branchActive, cap);
            ClearSlots(_capacity, cap);
            _capacity = cap;
        }

        void ClearSlots(int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                _parents[i] = -1;
                _firstChild[i] = -1;
                _nextSibling[i] = -1;
                _prevSibling[i] = -1;
                _childCount[i] = 0;
                _branchActive[i] = false;
            }
        }

        public readonly struct ChildList
        {
            readonly HierarchyContainer _hierarchy;
            readonly int _first;

            internal ChildList(HierarchyContainer hierarchy, int first)
            {
                _hierarchy = hierarchy;
                _first = first;
            }

            public Enumerator GetEnumerator() => new(_hierarchy, _hierarchy == null ? -1 : _first);

            public struct Enumerator
            {
                readonly HierarchyContainer _hierarchy;
                int _next;
                int _current;

                internal Enumerator(HierarchyContainer hierarchy, int first)
                {
                    _hierarchy = hierarchy;
                    _next = first;
                    _current = -1;
                }

                public EosEntity Current => _hierarchy.World.Entities.EntityFromId(_current);

                public bool MoveNext()
                {
                    if (_next < 0) return false;
                    _current = _next;
                    _next = _hierarchy._nextSibling[_next];
                    return true;
                }
            }
        }
    }
}
