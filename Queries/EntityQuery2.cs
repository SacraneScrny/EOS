using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Queries
{
    public readonly struct EntityQuery<T1, T2>
        where T1 : EosObject, new()
        where T2 : EosObject, new()
    {
        readonly IReadOnlyWorld _world;
        readonly Storage<T1> _s1;
        readonly Storage<T2> _s2;
        readonly QueryFilter _filter;

        internal EntityQuery(IReadOnlyWorld world)
        {
            _world = world;
            _s1 = world.ObjectsStorages.Get<T1>();
            _s2 = world.ObjectsStorages.Get<T2>();
            _filter = default;
        }

        EntityQuery(IReadOnlyWorld world, Storage<T1> s1, Storage<T2> s2, QueryFilter filter)
        {
            _world = world;
            _s1 = s1;
            _s2 = s2;
            _filter = filter;
        }

        public EntityQuery<T1, T2> With<TInclude>() where TInclude : EosObject, new()
            => new(_world, _s1, _s2, _filter.With(_world.ObjectsStorages.Get<TInclude>()));

        public EntityQuery<T1, T2> Without<TExclude>() where TExclude : EosObject, new()
            => new(_world, _s1, _s2, _filter.Without(_world.ObjectsStorages.Get<TExclude>()));

        public EntityQuery<T1, T2> WithTag(params object[] tags)
            => new(_world, _s1, _s2, _filter.Require(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T1, T2> WithoutTag(params object[] tags)
            => new(_world, _s1, _s2, _filter.Forbid(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T1, T2> WithAnyTag(params object[] tags)
            => new(_world, _s1, _s2, _filter.Any(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T1, T2> WithOneTag(params object[] tags)
            => new(_world, _s1, _s2, _filter.One(_world.Tags, _world.Tags.BuildMask(tags)));

        public Enumerator GetEnumerator() => new(_s1, _s2, _filter);

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

        public bool TryFirst(out QueryResult<T1, T2> result)
        {
            var e = GetEnumerator();
            if (e.MoveNext())
            {
                result = e.Current;
                return true;
            }
            result = default;
            return false;
        }

        public void ForEach(Action<T1, T2> action)
        {
            if (action == null) return;
            var e = GetEnumerator();
            while (e.MoveNext())
            {
                var r = e.Current;
                action(r.Item1, r.Item2);
            }
        }

        public void ForEach(Action<EosEntity, T1, T2> action)
        {
            if (action == null) return;
            var e = GetEnumerator();
            while (e.MoveNext())
            {
                var r = e.Current;
                action(r.Entity, r.Item1, r.Item2);
            }
        }

        public List<QueryResult<T1, T2>> ToList()
        {
            var list = new List<QueryResult<T1, T2>>();
            var e = GetEnumerator();
            while (e.MoveNext()) list.Add(e.Current);
            return list;
        }

        public struct Enumerator
        {
            readonly Storage<T1> _s1;
            readonly Storage<T2> _s2;
            readonly QueryFilter _filter;
            readonly bool _pivot1;
            readonly int _count;
            int _index;
            QueryResult<T1, T2> _current;

            internal Enumerator(Storage<T1> s1, Storage<T2> s2, QueryFilter filter)
            {
                _s1 = s1;
                _s2 = s2;
                _filter = filter;
                _pivot1 = s1.Count <= s2.Count;
                _count = _pivot1 ? s1.Count : s2.Count;
                _index = -1;
                _current = default;
            }

            public QueryResult<T1, T2> Current => _current;

            public bool MoveNext()
            {
                while (++_index < _count)
                {
                    EosEntity entity;
                    if (_pivot1)
                    {
                        if (!_s1.IsReady(_index)) continue;
                        entity = _s1.GetOwner(_index);
                        if (!_s2.TryGetReady(entity, out var c2)) continue;
                        if (!_filter.Matches(entity)) continue;
                        _current = new QueryResult<T1, T2>(entity, _s1.At(_index), c2);
                        return true;
                    }

                    if (!_s2.IsReady(_index)) continue;
                    entity = _s2.GetOwner(_index);
                    if (!_s1.TryGetReady(entity, out var c1)) continue;
                    if (!_filter.Matches(entity)) continue;
                    _current = new QueryResult<T1, T2>(entity, c1, _s2.At(_index));
                    return true;
                }
                return false;
            }
        }
    }
}
