using System;
using System.Collections.Generic;
using EOS.Entities;
using EOS.Objects;

namespace EOS.Storage
{
    internal static class StorageMap
    {
        static readonly Dictionary<Type, IStorage> _map = new();
        static readonly Dictionary<Type, List<IStorage>> _byInterface = new();

        public static Storage<T> Get<T>() where T : EosObject, new()
        {
            if (_map.TryGetValue(typeof(T), out var existing))
                return (Storage<T>)existing;

            var created = new Storage<T>();
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

        public static IStorage GetConcrete(Type type)
        {
            _map.TryGetValue(type, out var storage);
            return storage;
        }

        public static IReadOnlyList<IStorage> GetByInterface(Type interfaceType)
        {
            _byInterface.TryGetValue(interfaceType, out var result);
            return result;
        }

        public static void DestroyEntity(EosEntity entity)
        {
            foreach (var storage in _map.Values)
                storage.RemoveEntity(entity);
        }

        public static void Clear()
        {
            foreach (var storage in _map.Values)
                storage.Clear();
            _map.Clear();
            _byInterface.Clear();
        }
    }
}