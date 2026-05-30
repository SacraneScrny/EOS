using System;
using System.Collections.Generic;

using EOS.Storage;

namespace EOS.Entities
{
    internal static class EntitiesContainer
    {
        static int _next;
        static readonly Stack<int> _free = new();
        static readonly Dictionary<int, string> _names = new();
        static readonly Dictionary<int, ushort> _versions = new();
        static readonly Dictionary<int, bool> _actives = new();
        static readonly List<int> _alive = new();
        static readonly Dictionary<int, int> _aliveIndex = new();

        public static IEnumerable<EosEntity> All()
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                int id = _alive[i];
                yield return new EosEntity(id, _versions[id], _names.GetValueOrDefault(id, string.Empty));
            }
        }
        public static void ForEach(Action<EosEntity> action)
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                int id = _alive[i];
                action(new EosEntity(id, _versions[id], _names.GetValueOrDefault(id, string.Empty)));
            }
        }

        public static (int Id, ushort Version, string Name) Create(string name, bool active)
        {
            int id = _free.Count > 0 ? _free.Pop() : _next++;

            _versions.TryAdd(id, 0);
            _names[id] = name;
            _actives[id] = active;
            _aliveIndex[id] = _alive.Count;
            _alive.Add(id);

            return (id, _versions[id], name);
        }

        public static bool IsValid(EosEntity entity)
        {
            return entity.Id >= 0
                && _names.ContainsKey(entity.Id)
                && _versions.TryGetValue(entity.Id, out var version)
                && version == entity.Version;
        }
        public static bool IsActive(EosEntity entity)
        {
            return _actives.TryGetValue(entity.Id, out var active) && active;
        }
        public static void SetActive(EosEntity entity, bool active) => _actives[entity.Id] = active;

        public static void Destroy(EosEntity entity)
        {
            if (!IsValid(entity)) return;

            _names.Remove(entity.Id);
            _actives.Remove(entity.Id);

            if (_aliveIndex.TryGetValue(entity.Id, out int index))
            {
                int lastId = _alive[_alive.Count - 1];
                _alive[index] = lastId;
                _aliveIndex[lastId] = index;
                _alive.RemoveAt(_alive.Count - 1);
                _aliveIndex.Remove(entity.Id);
            }

            _versions[entity.Id]++;
            _free.Push(entity.Id);

            StorageMap.DestroyEntity(entity);
        }

        public static void Init()
        {
            _next = 0;
            _free.Clear();
            _names.Clear();
            _versions.Clear();
            _actives.Clear();
            _alive.Clear();
            _aliveIndex.Clear();
        }
    }
}
