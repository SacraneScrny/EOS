using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Logging;
using EOS.Objects;
using EOS.Objects.Interfaces;

namespace EOS.Storage
{
    /// <summary>Dense-array sparse-set storage for one component type <typeparamref name="T"/>; iterated linearly by systems and queries. Opt into instance pooling by marking <typeparamref name="T"/> with <see cref="EOS.Objects.Interfaces.IPoolableObject"/>.</summary>
    public class Storage<T> : WorldBound, IStorage, IIndexedStorage
        where T : EosObject, new()
    {
        const int InitialCapacity = 16;

        static readonly bool Poolable = typeof(IPoolableObject).IsAssignableFrom(typeof(T));
        readonly Stack<T> _pool = typeof(IPoolableObject).IsAssignableFrom(typeof(T)) ? new Stack<T>() : null;

        T[] _data = new T[InitialCapacity];
        int[] _owners = new int[InitialCapacity];
        ushort[] _ownerVersions = new ushort[InitialCapacity];
        ulong[] _addVersion = new ulong[InitialCapacity];
        ulong[] _markVersion = new ulong[InitialCapacity];
        ulong[] _markFrame = new ulong[InitialCapacity];
        bool[] _ready = new bool[InitialCapacity];
        int[] _sparse = new int[InitialCapacity];

        /// <summary>Number of components currently stored.</summary>
        public int Count { get; private set; }
        /// <summary>The contiguous dense span of components for linear iteration.</summary>
        public ReadOnlySpan<T> All => _data.AsSpan(0, Count);

        /// <summary>Monotonic high-water mark of add-versions, used to early-out <c>[New]</c> reactive scans.</summary>
        public ulong MaxAddVersion { get; private set; }
        /// <summary>Monotonic high-water mark of mark-versions, used to early-out <c>[Bumped]</c> reactive scans.</summary>
        public ulong MaxMarkVersion { get; private set; }
        /// <summary>The add-version stamped at the given dense index.</summary>
        public ulong AddVersionAt(int index) => _addVersion[index];
        /// <summary>The mark-version stamped at the given dense index.</summary>
        public ulong MarkVersionAt(int index) => _markVersion[index];

        /// <summary>The dense index of the entity's component, or -1 if absent.</summary>
        public int IndexOf(EosEntity entity)
        {
            int id = entity.Id;
            if (id < 0 || id >= _sparse.Length) return -1;
            int dense = _sparse[id];
            if (dense < Count && _owners[dense] == id && _ownerVersions[dense] == entity.Version)
                return dense;
            return -1;
        }

        /// <summary>Adds a component for the entity (returning the existing one if present), rents from the pool when poolable. Guarded against mid-iteration structural changes.</summary>
        public T Add(EosEntity entity)
        {
            int existing = IndexOf(entity);
            if (existing >= 0) return _data[existing];

            World.GuardStructuralChange($"Add<{typeof(T).Name}>");

            EnsureData(Count + 1);
            EnsureSparse(entity.Id);

            int i = Count++;
            _owners[i] = entity.Id;
            _ownerVersions[i] = entity.Version;
            _addVersion[i] = 0;
            _markVersion[i] = 0;
            _markFrame[i] = 0;
            _ready[i] = false;
            _sparse[entity.Id] = i;
            _data[i] = (Poolable && _pool.Count > 0) ? _pool.Pop() : new T();
            _data[i].SetupObject(entity);

            World.ObjectsStorages.TrackEntity(entity, this);
            return _data[i];
        }

        /// <summary>Returns the entity's component; logs an error and returns null if the entity has no component here.</summary>
        public T Get(EosEntity entity)
        {
            try
            {
                int i = IndexOf(entity);
                if (i < 0) throw new InvalidOperationException($"Entity {entity.Id} not found in Storage<{typeof(T).Name}>");
                return _data[i];
            }
            catch (Exception ex)
            {
                EosLog.Error(ex.Message, nameof(Storage<T>));
                return null;
            }
        }
        /// <summary>Tries to get the entity's component, regardless of ready state.</summary>
        public bool TryGet(EosEntity entity, out T result)
        {
            int i = IndexOf(entity);
            if (i >= 0)
            {
                result = _data[i];
                return true;
            }
            result = null;
            return false;
        }
        /// <summary>The component at the given dense index.</summary>
        public T At(int index) => _data[index];
        /// <summary>Whether the entity has a component in this storage.</summary>
        public bool Has(EosEntity entity) => IndexOf(entity) >= 0;
        /// <summary>Whether the entity has a ready (awoken, started, enabled) component in this storage.</summary>
        public bool HasReady(EosEntity entity)
        {
            int i = IndexOf(entity);
            return i >= 0 && IsReady(i);
        }
        /// <summary>Tries to get the entity's component only if it is ready.</summary>
        public bool TryGetReady(EosEntity entity, out T result)
        {
            int i = IndexOf(entity);
            if (i >= 0 && IsReady(i))
            {
                result = _data[i];
                return true;
            }
            result = null;
            return false;
        }
        /// <summary>The entity owning the component at the given dense index.</summary>
        public EosEntity GetOwner(int index) =>
            new(_owners[index], _ownerVersions[index], World);

        /// <summary>Removes and disposes the entity's component (returning it to the pool if poolable). Guarded against mid-iteration structural changes.</summary>
        public bool Remove(EosEntity entity)
        {
            int i = IndexOf(entity);
            if (i < 0) return false;
            if (!World.GuardStructuralChange($"Remove<{typeof(T).Name}>")) return false;
            var toDispose = _data[i];
            int last = --Count;

            if (i != last)
            {
                _data[i] = _data[last];
                _owners[i] = _owners[last];
                _ownerVersions[i] = _ownerVersions[last];
                _addVersion[i] = _addVersion[last];
                _markVersion[i] = _markVersion[last];
                _markFrame[i] = _markFrame[last];
                _ready[i] = _ready[last];
                _sparse[_owners[i]] = i;
            }

            toDispose.Dispose();
            if (Poolable)
            {
                toDispose.ResetForReuse();
                _pool.Push(toDispose);
            }
            _data[last] = null;
            _owners[last] = 0;
            _ownerVersions[last] = 0;
            _addVersion[last] = 0;
            _markVersion[last] = 0;
            _markFrame[last] = 0;
            _ready[last] = false;

            World.ObjectsStorages.UntrackEntity(entity, this);
            return true;
        }

        /// <summary>Removes the entity's component, if present (non-generic <see cref="IStorage"/> entry point).</summary>
        public void RemoveEntity(EosEntity entity) => Remove(entity);
        EosObject IStorage.AddObject(EosEntity entity) => Add(entity);

        /// <summary>Disposes every component, resets the dense arrays and watermarks, and drains the pool.</summary>
        public void Clear()
        {
            int count = Count;
            for (int i = count - 1; i >= 0; i--)
            {
                var obj = _data[i];
                if (obj != null && !obj.IsDisposed)
                    obj.Dispose();
            }
            Array.Clear(_data, 0, count);
            Array.Clear(_owners, 0, count);
            Array.Clear(_ownerVersions, 0, count);
            Array.Clear(_addVersion, 0, count);
            Array.Clear(_markVersion, 0, count);
            Array.Clear(_markFrame, 0, count);
            Array.Clear(_ready, 0, count);
            Count = 0;
            MaxAddVersion = 0;
            MaxMarkVersion = 0;
            if (Poolable) _pool.Clear();
        }

        /// <summary>Whether the component at the given dense index is ready (awoken, started, enabled).</summary>
        public bool IsReady(int index) => _ready[index];

        /// <summary>Recomputes the ready flag for the entity's component after an enabled/active change.</summary>
        public void RefreshReady(EosEntity entity)
        {
            int i = IndexOf(entity);
            if (i < 0) return;
            _ready[i] = _data[i] != null && _data[i].IsEnabled;
        }

        /// <summary>Stamps the add-version for the entity's component, signalling the <c>[New]</c> channel, and refreshes readiness.</summary>
        public void MarkReady(EosEntity entity)
        {
            int i = IndexOf(entity);
            if (i < 0) return;
            _addVersion[i] = World.NextVersion();
            MaxAddVersion = _addVersion[i];
            _ready[i] = _data[i] != null && _data[i].IsEnabled;
        }

        /// <summary>Stamps the mark-version for the entity's component, signalling the <c>[Bumped]</c> channel (deduped to once per frame).</summary>
        public void Bump(EosEntity entity)
        {
            int i = IndexOf(entity);
            if (i < 0) return;
            ulong frame = World.Frame;
            if (_markVersion[i] != 0 && _markFrame[i] == frame) return;
            _markVersion[i] = World.NextVersion();
            _markFrame[i] = frame;
            MaxMarkVersion = _markVersion[i];
        }

        void EnsureData(int count)
        {
            if (count <= _data.Length) return;
            int n = _data.Length * 2;
            while (n < count) n *= 2;
            Array.Resize(ref _data, n);
            Array.Resize(ref _owners, n);
            Array.Resize(ref _ownerVersions, n);
            Array.Resize(ref _addVersion, n);
            Array.Resize(ref _markVersion, n);
            Array.Resize(ref _markFrame, n);
            Array.Resize(ref _ready, n);
        }
        void EnsureSparse(int id)
        {
            if (id < _sparse.Length) return;
            int n = _sparse.Length * 2;
            while (n <= id) n *= 2;
            Array.Resize(ref _sparse, n);
        }

        object IIndexedStorage.GetAt(int index) => _data[index];
        object IIndexedStorage.TryGetObject(EosEntity entity)
            => TryGet(entity, out var result) ? result : null;
        object IIndexedStorage.TryGetReadyObject(EosEntity entity)
            => TryGetReady(entity, out var result) ? result : null;
        EosEntity IIndexedStorage.GetOwner(int index) => GetOwner(index);
        int IIndexedStorage.Count => Count;
    }
}