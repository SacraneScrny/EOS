using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Storage;

namespace EOS.Entities
{
    public class EntitiesContainer : WorldBound
    {
        int _next;
        readonly Stack<int> _free = new();
        ushort[] _versions = new ushort[1024];
        string[] _names = new string[1024];
        bool[] _actives = new bool[1024];
        bool[] _exists = new bool[1024];
        bool[] _serializable = new bool[1024];
        readonly List<int> _alive = new();
        int[] _aliveIndex = new int[1024];
        readonly Dictionary<string, int> _keyToId = new();
        readonly Dictionary<int, string> _idToKey = new();

        internal int AliveCount => _alive.Count;
        internal EosEntity At(int index)
        {
            int id = _alive[index];
            return new EosEntity(id, _versions[id], World, _names[id] ?? string.Empty);
        }
        internal string GetName(int id)
        {
            if (id < 0 || id >= _names.Length) return string.Empty;
            return _names[id] ?? string.Empty;
        }

        public AliveEntities All() => new(this);
        public void ForEach(Action<EosEntity> action)
        {
            for (int i = 0; i < _alive.Count; i++)
                action(At(i));
        }

        internal (int Id, ushort Version, string Name) Create(string name, bool active, bool isSerializable = true)
        {
            World.GuardStructuralChange($"Create entity '{name}'");

            int id = _free.Count > 0 ? _free.Pop() : _next++;
            EnsureCapacity(id);

            _names[id] = name;
            _actives[id] = active;
            _exists[id] = true;
            _serializable[id] = isSerializable;
            _aliveIndex[id] = _alive.Count;
            _alive.Add(id);

            return (id, _versions[id], name);
        }

        public bool IsSerializable(EosEntity entity)
        {
            int id = entity.Id;
            return id >= 0 && id < _serializable.Length && _exists[id] && _serializable[id];
        }

        public bool TryFind(string key, out EosEntity entity)
        {
            if (!string.IsNullOrEmpty(key) && _keyToId.TryGetValue(key, out int id))
            {
                entity = new EosEntity(id, _versions[id], World, _names[id] ?? string.Empty);
                return true;
            }
            entity = EosEntity.Null;
            return false;
        }

        public void SetStableKey(EosEntity entity, string key)
        {
            if (!IsValid(entity)) return;
            int id = entity.Id;
            if (_idToKey.TryGetValue(id, out var old)) _keyToId.Remove(old);
            if (!string.IsNullOrEmpty(key)) { _keyToId[key] = id; _idToKey[id] = key; }
            else _idToKey.Remove(id);
        }

        public string GetStableKey(EosEntity entity)
        {
            if (!IsValid(entity)) return null;
            _idToKey.TryGetValue(entity.Id, out var key);
            return key;
        }

        public bool IsValid(EosEntity entity)
        {
            int id = entity.Id;
            return id >= 0 && id < _exists.Length && _exists[id] && _versions[id] == entity.Version;
        }
        public bool IsActive(EosEntity entity)
        {
            int id = entity.Id;
            return id >= 0 && id < _exists.Length && _exists[id] && _actives[id];
        }
        public void SetActive(EosEntity entity, bool active)
        {
            if (!IsValid(entity)) return;
            _actives[entity.Id] = active;
        }

        public void Destroy(EosEntity entity)
        {
            if (!IsValid(entity)) return;
            if (!World.GuardStructuralChange($"Destroy entity '{GetName(entity.Id)}'")) return;
            int id = entity.Id;

            if (_idToKey.TryGetValue(id, out var stableKey))
            {
                _keyToId.Remove(stableKey);
                _idToKey.Remove(id);
            }

            int index = _aliveIndex[id];
            int lastId = _alive[^1];
            _alive[index] = lastId;
            _aliveIndex[lastId] = index;
            _alive.RemoveAt(_alive.Count - 1);

            _exists[id] = false;
            _names[id] = null;
            _actives[id] = false;
            _versions[id]++;
            _free.Push(id);

            World.ObjectsStorages.DestroyEntity(entity);
        }

        void EnsureCapacity(int id)
        {
            if (id < _versions.Length) return;
            int n = _versions.Length * 2;
            while (n <= id) n *= 2;
            Array.Resize(ref _versions, n);
            Array.Resize(ref _names, n);
            Array.Resize(ref _actives, n);
            Array.Resize(ref _exists, n);
            Array.Resize(ref _serializable, n);
            Array.Resize(ref _aliveIndex, n);
        }

        internal void Reset()
        {
            _next = 0;
            _free.Clear();
            Array.Clear(_versions, 0, _versions.Length);
            Array.Clear(_names, 0, _names.Length);
            Array.Clear(_actives, 0, _actives.Length);
            Array.Clear(_exists, 0, _exists.Length);
            Array.Clear(_serializable, 0, _serializable.Length);
            Array.Clear(_aliveIndex, 0, _aliveIndex.Length);
            _alive.Clear();
            _keyToId.Clear();
            _idToKey.Clear();
        }

        public readonly struct AliveEntities
        {
            readonly EntitiesContainer _c;
            public AliveEntities(EntitiesContainer c) => _c = c;
            public Enumerator GetEnumerator() => new(_c);

            public struct Enumerator
            {
                readonly EntitiesContainer _c;
                int _i;
                public Enumerator(EntitiesContainer c) { _c = c; _i = -1; }
                public EosEntity Current => _c.At(_i);
                public bool MoveNext() => ++_i < _c.AliveCount;
            }
        }
    }
}
