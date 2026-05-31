using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Objects;

namespace EOS.Storage
{
    public class Storage<T> : WorldBound, IStorage, IIndexedStorage
        where T : EosObject, new()
    {
        T[] _data = new T[1024];
        int[] _owners = new int[1024];
        ushort[] _ownerVersions = new ushort[1024];
        int[] _addedFrame = new int[1024];
        int[] _sparse = new int[1024];
        int _frame;
        readonly List<EosEntity> _recent = new();

        public int Count { get; private set; }
        public ReadOnlySpan<T> All => _data.AsSpan(0, Count);
        public IReadOnlyList<EosEntity> RecentlyAdded => _recent;

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

            EnsureData(Count + 1);
            EnsureSparse(entity.Id);

            int i = Count++;
            _owners[i] = entity.Id;
            _ownerVersions[i] = entity.Version;
            _addedFrame[i] = -1;
            _sparse[entity.Id] = i;
            _data[i] = new T();
            _data[i].SetupObject(entity);

            World.ObjectsStorages.TrackEntity(entity, this);
            return _data[i];
        }

        public T Get(EosEntity entity) => _data[IndexOf(entity)];
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
        public bool Has(EosEntity entity) => IndexOf(entity) >= 0;
        public EosEntity GetOwner(int index) =>
            new(_owners[index], _ownerVersions[index], World, World.Entities.GetName(_owners[index]));

        public bool IsRecent(EosEntity entity)
        {
            int i = IndexOf(entity);
            return i >= 0 && _addedFrame[i] == _frame;
        }

        public bool Remove(EosEntity entity)
        {
            int i = IndexOf(entity);
            if (i < 0) return false;
            var toDispose = _data[i];
            int last = --Count;

            if (i != last)
            {
                _data[i] = _data[last];
                _owners[i] = _owners[last];
                _ownerVersions[i] = _ownerVersions[last];
                _addedFrame[i] = _addedFrame[last];
                _sparse[_owners[i]] = i;
            }

            toDispose.Dispose();
            _data[last] = null;
            _owners[last] = 0;
            _ownerVersions[last] = 0;
            _addedFrame[last] = 0;

            World.ObjectsStorages.UntrackEntity(entity, this);
            return true;
        }

        public void RemoveEntity(EosEntity entity) => Remove(entity);

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
            Array.Clear(_addedFrame, 0, count);
            Count = 0;
            _frame = 0;
            _recent.Clear();
        }

        public void ClearRecent()
        {
            _frame++;
            _recent.Clear();
        }

        public bool IsReady(int index) => _data[index] != null && _data[index].IsEnabled;

        public void MarkReady(EosEntity entity)
        {
            int i = IndexOf(entity);
            if (i < 0) return;
            if (_addedFrame[i] == _frame) return;
            _addedFrame[i] = _frame;
            _recent.Add(entity);
        }

        void EnsureData(int count)
        {
            if (count <= _data.Length) return;
            int n = _data.Length * 2;
            while (n < count) n *= 2;
            Array.Resize(ref _data, n);
            Array.Resize(ref _owners, n);
            Array.Resize(ref _ownerVersions, n);
            Array.Resize(ref _addedFrame, n);
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
        EosEntity IIndexedStorage.GetOwner(int index) => GetOwner(index);
        int IIndexedStorage.Count => Count;
    }
}
