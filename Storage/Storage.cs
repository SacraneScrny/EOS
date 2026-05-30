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
        readonly Dictionary<ulong, int> _index = new();

        public int Count { get; private set; }
        public ReadOnlySpan<T> All => _data.AsSpan(0, Count);

        static ulong Key(EosEntity entity) => ((ulong)entity.Version << 32) | (uint)entity.Id;

        public T Add(EosEntity entity)
        {
            var key = Key(entity);
            if (_index.TryGetValue(key, out var existing))
                return _data[existing];

            if (Count >= _data.Length)
            {
                Array.Resize(ref _data, _data.Length * 2);
                Array.Resize(ref _owners, _owners.Length * 2);
                Array.Resize(ref _ownerVersions, _ownerVersions.Length * 2);
            }

            int i = Count++;
            _owners[i] = entity.Id;
            _ownerVersions[i] = entity.Version;
            _index[key] = i;
            _data[i] = new T();
            _data[i].SetupObject(entity);
            return _data[i];
        }

        public T Get(EosEntity entity) => _data[_index[Key(entity)]];
        public bool TryGet(EosEntity entity, out T result)
        {
            if (_index.TryGetValue(Key(entity), out var i))
            {
                result = _data[i];
                return true;
            }
            result = null;
            return false;
        }
        public bool Has(EosEntity entity) => _index.ContainsKey(Key(entity));
        public EosEntity GetOwner(int index) => new(_owners[index], _ownerVersions[index], World);

        public bool Remove(EosEntity entity)
        {
            var key = Key(entity);
            if (!_index.TryGetValue(key, out var i)) return false;
            var toDispose = _data[i];
            int last = --Count;

            if (i != last)
            {
                _data[i] = _data[last];
                _owners[i] = _owners[last];
                _ownerVersions[i] = _ownerVersions[last];
                _index[((ulong)_ownerVersions[i] << 32) | (uint)_owners[i]] = i;
            }

            _index.Remove(key);
            toDispose.Dispose(); 
            _data[last] = null;
            _owners[last] = 0;
            _ownerVersions[last] = 0;

            return true;
        }

        public void RemoveEntity(EosEntity entity) => Remove(entity);
        public void Clear()
        {
            _data = new T[1024];
            _owners = new int[1024];
            _ownerVersions = new ushort[1024];
            _index.Clear();
            Count = 0;
        }
        
        object IIndexedStorage.GetAt(int index) => _data[index];
        object IIndexedStorage.TryGetObject(EosEntity entity)
            => TryGet(entity, out var result) ? result : null;
    
        EosEntity IIndexedStorage.GetOwner(int index) => GetOwner(index);
        int IIndexedStorage.Count => Count;
    }
}
