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

        public Storage<T> Get<T>() where T : EosObject, new()
        {
            if (_map.TryGetValue(typeof(T), out var existing))
                return (Storage<T>)existing;

            var created = new Storage<T>();
            created.Init(World);
            _map.Add(typeof(T), created);

            foreach (var iface in typeof(T).GetInterfaces())
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
            _map.Clear();
            _byInterface.Clear();
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
