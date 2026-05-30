using System;
using System.Collections.Generic;

using EOS.Entities;
using EOS.Objects;

namespace EOS.Storage
{
    internal static class StorageMap
    {
        static readonly Dictionary<Type, IStorage> _map = new();

        public static Storage<T> Get<T>() where T : EosObject, new()
        {
            if (_map.TryGetValue(typeof(T), out var storage))
                return (Storage<T>)storage;

            var created = new Storage<T>();
            _map.Add(typeof(T), created);
            return created;
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
        }
    }
}
