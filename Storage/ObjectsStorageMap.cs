using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Entities;
using EOS.Objects;

namespace EOS.Storage
{
    public class ObjectsStorageMap : WorldBound
    {
        readonly Dictionary<Type, IStorage> _map = new();
        readonly Dictionary<Type, List<IStorage>> _byInterface = new();
        List<IStorage>[] _entityStorages = new List<IStorage>[1024];
        readonly Stack<List<IStorage>> _listPool = new();

        public IReadOnlyDictionary<Type, IStorage> AllStorages => _map;

        public Storage<T> Get<T>() where T : EosObject, new()
            => (Storage<T>)GetOrCreate(typeof(T));

        public IStorage GetOrCreate(Type type)
        {
            if (type != null && _map.TryGetValue(type, out var existing))
                return existing;

            if (type == null || type.IsAbstract || !typeof(EosObject).IsAssignableFrom(type))
                throw new ArgumentException($"GetOrCreate requires a concrete EosObject type, got '{type}'");

            var created = (IStorage)Activator.CreateInstance(typeof(Storage<>).MakeGenericType(type));
            ((WorldBound)created).Init(World);
            _map.Add(type, created);

            foreach (var iface in type.GetInterfaces())
            {
                if (!_byInterface.TryGetValue(iface, out var list))
                {
                    list = new List<IStorage>();
                    _byInterface[iface] = list;
                }
                list.Add(created);
            }

            return created;
        }
        public IStorage GetConcrete(Type type)
        {
            _map.TryGetValue(type, out var storage);
            return storage;
        }
        public IReadOnlyList<IStorage> GetByInterface(Type interfaceType)
        {
            _byInterface.TryGetValue(interfaceType, out var result);
            return result;
        }

        internal void TrackEntity(EosEntity entity, IStorage storage)
        {
            int id = entity.Id;
            if (id < 0) return;
            EnsureCapacity(id);
            var list = _entityStorages[id];
            if (list == null)
            {
                list = _listPool.Count > 0 ? _listPool.Pop() : new List<IStorage>();
                _entityStorages[id] = list;
            }
            list.Add(storage);
        }
        internal void UntrackEntity(EosEntity entity, IStorage storage)
        {
            int id = entity.Id;
            if (id < 0 || id >= _entityStorages.Length) return;
            var list = _entityStorages[id];
            if (list == null) return;
            list.Remove(storage);
            if (list.Count == 0)
            {
                _entityStorages[id] = null;
                _listPool.Push(list);
            }
        }

        internal bool RemoveFromStorage(EosObject obj)
        {
            if (obj == null) return false;
            if (!_map.TryGetValue(obj.GetType(), out var storage)) return false;
            storage.RemoveEntity(obj.Entity);
            return true;
        }

        internal void MarkReady(EosObject obj)
        {
            if (_map.TryGetValue(obj.GetType(), out var storage))
                (storage as IIndexedStorage)?.MarkReady(obj.Entity);
        }
        internal void Bump(EosObject obj)
        {
            if (_map.TryGetValue(obj.GetType(), out var storage))
                (storage as IIndexedStorage)?.Bump(obj.Entity);
        }
        internal void RefreshReady(EosObject obj)
        {
            if (_map.TryGetValue(obj.GetType(), out var storage))
                (storage as IIndexedStorage)?.RefreshReady(obj.Entity);
        }
        internal void RefreshReadyAll(EosEntity entity)
        {
            int id = entity.Id;
            if (id < 0 || id >= _entityStorages.Length) return;
            var list = _entityStorages[id];
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                (list[i] as IIndexedStorage)?.RefreshReady(entity);
        }

        internal void DestroyEntity(EosEntity entity)
        {
            int id = entity.Id;
            if (id < 0 || id >= _entityStorages.Length) return;
            var list = _entityStorages[id];
            if (list == null) return;
            _entityStorages[id] = null;
            for (int i = 0; i < list.Count; i++)
                list[i].RemoveEntity(entity);
            list.Clear();
            _listPool.Push(list);
        }
        internal void Reset()
        {
            foreach (var storage in _map.Values)
                storage.Clear();
            Array.Clear(_entityStorages, 0, _entityStorages.Length);
            _listPool.Clear();
        }

        void EnsureCapacity(int id)
        {
            if (id < _entityStorages.Length) return;
            int n = _entityStorages.Length * 2;
            while (n <= id) n *= 2;
            Array.Resize(ref _entityStorages, n);
        }
    }
}
