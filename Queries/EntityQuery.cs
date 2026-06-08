using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Queries
{
    public readonly struct EntityQuery<T>
        where T : EosObject, new()
    {
        readonly IReadOnlyWorld _world;
        readonly Storage<T> _storage;
        readonly QueryFilter _filter;

        internal EntityQuery(IReadOnlyWorld world)
        {
            _world = world;
            _storage = world.ObjectsStorages.Get<T>();
            _filter = default;
        }

        EntityQuery(IReadOnlyWorld world, Storage<T> storage, QueryFilter filter)
        {
            _world = world;
            _storage = storage;
            _filter = filter;
        }

        public EntityQuery<T> With<TInclude>() where TInclude : EosObject, new()
            => new(_world, _storage, _filter.With(_world.ObjectsStorages.Get<TInclude>()));

        public EntityQuery<T> Without<TExclude>() where TExclude : EosObject, new()
            => new(_world, _storage, _filter.Without(_world.ObjectsStorages.Get<TExclude>()));

        public EntityQuery<T> WithTag(params object[] tags)
            => new(_world, _storage, _filter.Require(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T> WithoutTag(params object[] tags)
            => new(_world, _storage, _filter.Forbid(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T> WithAnyTag(params object[] tags)
            => new(_world, _storage, _filter.Any(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T> WithOneTag(params object[] tags)
            => new(_world, _storage, _filter.One(_world.Tags, _world.Tags.BuildMask(tags)));

        public Enumerator GetEnumerator() => new(_storage, _filter);

        public bool Any()
        {
            var e = GetEnumerator();
            return e.MoveNext();
        }

        public int Count()
        {
            int n = 0;
            var e = GetEnumerator();
            while (e.MoveNext()) n++;
            return n;
        }

        public T First()
        {
            var e = GetEnumerator();
            return e.MoveNext() ? e.Current : null;
        }

        public bool TryFirst(out T result)
        {
            var e = GetEnumerator();
            if (e.MoveNext())
            {
                result = e.Current;
                return true;
            }
            result = null;
            return false;
        }

        public void ForEach(Action<T> action)
        {
            if (action == null) return;
            var e = GetEnumerator();
            while (e.MoveNext()) action(e.Current);
        }

        public List<T> ToList()
        {
            var list = new List<T>();
            var e = GetEnumerator();
            while (e.MoveNext()) list.Add(e.Current);
            return list;
        }

        public struct Enumerator
        {
            readonly Storage<T> _storage;
            readonly QueryFilter _filter;
            readonly int _count;
            int _index;

            internal Enumerator(Storage<T> storage, QueryFilter filter)
            {
                _storage = storage;
                _filter = filter;
                _count = storage.Count;
                _index = -1;
            }

            public T Current => _storage.At(_index);

            public bool MoveNext()
            {
                while (++_index < _count)
                {
                    if (!_storage.IsReady(_index)) continue;
                    if (_filter.Matches(_storage.GetOwner(_index))) return true;
                }
                return false;
            }
        }
    }
}
