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

        internal void MarkReady(EosObject obj)
        {
            if (_map.TryGetValue(obj.GetType(), out var storage))
                (storage as IIndexedStorage)?.MarkReady(obj.Entity);
        }
        internal void ClearAllRecent()
        {
            foreach (var storage in _map.Values)
                storage.ClearRecent();
        }

        internal void DestroyEntity(EosEntity entity)
        {
            foreach (var storage in _map.Values)
                storage.RemoveEntity(entity);
        }
        internal void Reset()
        {
            foreach (var storage in _map.Values)
                storage.Clear();
            _map.Clear();
            _byInterface.Clear();
        }
    }
}