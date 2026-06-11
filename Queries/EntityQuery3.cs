using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Queries
{
    public readonly struct EntityQuery<T1, T2, T3>
        where T1 : EosObject, new()
        where T2 : EosObject, new()
        where T3 : EosObject, new()
    {
        readonly IReadOnlyWorld _world;
        readonly Storage<T1> _s1;
        readonly Storage<T2> _s2;
        readonly Storage<T3> _s3;
        readonly QueryFilter _filter;

        internal EntityQuery(IReadOnlyWorld world)
        {
            _world = world;
            _s1 = world.ObjectsStorages.Get<T1>();
            _s2 = world.ObjectsStorages.Get<T2>();
            _s3 = world.ObjectsStorages.Get<T3>();
            _filter = default;
        }

        EntityQuery(IReadOnlyWorld world, Storage<T1> s1, Storage<T2> s2, Storage<T3> s3, QueryFilter filter)
        {
            _world = world;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _filter = filter;
        }

        public EntityQuery<T1, T2, T3> With<TInclude>() where TInclude : EosObject, new()
            => new(_world, _s1, _s2, _s3, _filter.With(_world.ObjectsStorages.Get<TInclude>()));

        public EntityQuery<T1, T2, T3> Without<TExclude>() where TExclude : EosObject, new()
            => new(_world, _s1, _s2, _s3, _filter.Without(_world.ObjectsStorages.Get<TExclude>()));

        public EntityQuery<T1, T2, T3> WithTag(params object[] tags)
            => new(_world, _s1, _s2, _s3, _filter.Require(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T1, T2, T3> WithoutTag(params object[] tags)
            => new(_world, _s1, _s2, _s3, _filter.Forbid(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T1, T2, T3> WithAnyTag(params object[] tags)
            => new(_world, _s1, _s2, _s3, _filter.Any(_world.Tags, _world.Tags.BuildMask(tags)));

        public EntityQuery<T1, T2, T3> WithOneTag(params object[] tags)
            => new(_world, _s1, _s2, _s3, _filter.One(_world.Tags, _world.Tags.BuildMask(tags)));

        public Enumerator GetEnumerator() => new(_world, _s1, _s2, _s3, _filter);

        public bool Any()
        {
            using var e = GetEnumerator();
            return e.MoveNext();
        }

        public int Count()
        {
            int n = 0;
            using var e = GetEnumerator();
            while (e.MoveNext()) n++;
            return n;
        }

        public bool TryFirst(out QueryResult<T1, T2, T3> result)
        {
            using var e = GetEnumerator();
            if (e.MoveNext())
            {
                result = e.Current;
                return true;
            }
            result = default;
            return false;
        }

        public void ForEach(Action<T1, T2, T3> action)
        {
            if (action == null) return;
            using var e = GetEnumerator();
            while (e.MoveNext())
            {
                var r = e.Current;
                action(r.Item1, r.Item2, r.Item3);
            }
        }

        public void ForEach(Action<EosEntity, T1, T2, T3> action)
        {
            if (action == null) return;
            using var e = GetEnumerator();
            while (e.MoveNext())
            {
                var r = e.Current;
                action(r.Entity, r.Item1, r.Item2, r.Item3);
            }
        }

        public List<QueryResult<T1, T2, T3>> ToList()
        {
            var list = new List<QueryResult<T1, T2, T3>>();
            using var e = GetEnumerator();
            while (e.MoveNext()) list.Add(e.Current);
            return list;
        }

        public struct Enumerator : IDisposable
        {
            readonly IReadOnlyWorld _world;
            readonly Storage<T1> _s1;
            readonly Storage<T2> _s2;
            readonly Storage<T3> _s3;
            readonly QueryFilter _filter;
            readonly int _pivot;
            readonly int _count;
            int _index;
            QueryResult<T1, T2, T3> _current;

            bool _isDisposed;

            internal Enumerator(IReadOnlyWorld world, Storage<T1> s1, Storage<T2> s2, Storage<T3> s3, QueryFilter filter)
            {
                _isDisposed = false;

                _world = world;
                _s1 = s1;
                _s2 = s2;
                _s3 = s3;
                _filter = filter;

                int c1 = s1.Count;
                int c2 = s2.Count;
                int c3 = s3.Count;
                if (c1 <= c2 && c1 <= c3) { _pivot = 0; _count = c1; }
                else if (c2 <= c3) { _pivot = 1; _count = c2; }
                else { _pivot = 2; _count = c3; }

                _index = -1;
                _current = default;

                _world.BeginIterationInternal();
            }

            public QueryResult<T1, T2, T3> Current => _current;

            public bool MoveNext()
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(Enumerator));
                while (++_index < _count)
                {
                    EosEntity entity;
                    T1 a;
                    T2 b;
                    T3 c;

                    if (_pivot == 0)
                    {
                        if (!_s1.IsReady(_index)) continue;
                        entity = _s1.GetOwner(_index);
                        a = _s1.At(_index);
                        if (!_s2.TryGetReady(entity, out b)) continue;
                        if (!_s3.TryGetReady(entity, out c)) continue;
                    }
                    else if (_pivot == 1)
                    {
                        if (!_s2.IsReady(_index)) continue;
                        entity = _s2.GetOwner(_index);
                        b = _s2.At(_index);
                        if (!_s1.TryGetReady(entity, out a)) continue;
                        if (!_s3.TryGetReady(entity, out c)) continue;
                    }
                    else
                    {
                        if (!_s3.IsReady(_index)) continue;
                        entity = _s3.GetOwner(_index);
                        c = _s3.At(_index);
                        if (!_s1.TryGetReady(entity, out a)) continue;
                        if (!_s2.TryGetReady(entity, out b)) continue;
                    }

                    if (!_filter.Matches(entity)) continue;
                    _current = new QueryResult<T1, T2, T3>(entity, a, b, c);
                    return true;
                }
                return false;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    _world.EndIterationInternal();
                }
            }
        }
    }
}
