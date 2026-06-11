using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Logging;
using EOS.Objects;
using EOS.Objects.Interfaces;

namespace EOS.Storage
{
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

        public int Count { get; private set; }
        public ReadOnlySpan<T> All => _data.AsSpan(0, Count);

        public ulong MaxAddVersion { get; private set; }
        public ulong MaxMarkVersion { get; private set; }
        public ulong AddVersionAt(int index) => _addVersion[index];
        public ulong MarkVersionAt(int index) => _markVersion[index];

        public int IndexOf(EosEntity entity)
        {
            int id = entity.Id;
            if (id < 0 || id >= _sparse.Length) return -1;
            int dense = _sparse[id];
            if (dense < Count && _owners[dense] == id && _ownerVersions[dense] == entity.Version)
                return dense;
            return -1;
        }

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
        public T At(int index) => _data[index];
        public bool Has(EosEntity entity) => IndexOf(entity) >= 0;
        public bool HasReady(EosEntity entity)
        {
            int i = IndexOf(entity);
            return i >= 0 && IsReady(i);
        }
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
        public EosEntity GetOwner(int index) =>
            new(_owners[index], _ownerVersions[index], World);

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

        public void RemoveEntity(EosEntity entity) => Remove(entity);
        EosObject IStorage.AddObject(EosEntity entity) => Add(entity);

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

        public bool IsReady(int index) => _ready[index];

        public void RefreshReady(EosEntity entity)
        {
            int i = IndexOf(entity);
            if (i < 0) return;
            _ready[i] = _data[i] != null && _data[i].IsEnabled;
        }

        public void MarkReady(EosEntity entity)
        {
            int i = IndexOf(entity);
            if (i < 0) return;
            _addVersion[i] = World.NextVersion();
            MaxAddVersion = _addVersion[i];
            _ready[i] = _data[i] != null && _data[i].IsEnabled;
        }

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