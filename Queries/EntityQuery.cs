using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Queries
{
    /// <summary>Immutable, allocation-free query over a single ready, enabled component type; obtain via <see cref="WorldQueries.Query{T}"/> and enumerate with <c>foreach</c>.</summary>
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

        /// <summary>Returns a new query that also requires the entity to carry a ready <typeparamref name="TInclude"/>.</summary>
        public EntityQuery<T> With<TInclude>() where TInclude : EosObject, new()
            => new(_world, _storage, _filter.With(_world.ObjectsStorages.Get<TInclude>()));

        /// <summary>Returns a new query that excludes entities carrying a ready <typeparamref name="TExclude"/>.</summary>
        public EntityQuery<T> Without<TExclude>() where TExclude : EosObject, new()
            => new(_world, _storage, _filter.Without(_world.ObjectsStorages.Get<TExclude>()));

        /// <summary>Returns a new query requiring all of the given tags (strings or enum values).</summary>
        public EntityQuery<T> WithTag(params object[] tags)
            => new(_world, _storage, _filter.Require(_world.Tags, _world.Tags.BuildMask(tags)));

        /// <summary>Returns a new query excluding entities that carry any of the given tags.</summary>
        public EntityQuery<T> WithoutTag(params object[] tags)
            => new(_world, _storage, _filter.Forbid(_world.Tags, _world.Tags.BuildMask(tags)));

        /// <summary>Returns a new query requiring at least one of the given tags.</summary>
        public EntityQuery<T> WithAnyTag(params object[] tags)
            => new(_world, _storage, _filter.Any(_world.Tags, _world.Tags.BuildMask(tags)));

        /// <summary>Returns a new query requiring exactly one of the given tags.</summary>
        public EntityQuery<T> WithOneTag(params object[] tags)
            => new(_world, _storage, _filter.One(_world.Tags, _world.Tags.BuildMask(tags)));

        /// <summary>Returns the allocation-free struct enumerator; supports <c>foreach</c>.</summary>
        public Enumerator GetEnumerator() => new(_world, _storage, _filter);

        /// <summary>True if at least one component matches the query.</summary>
        public bool Any()
        {
            using var e = GetEnumerator();
            return e.MoveNext();
        }

        /// <summary>Counts the matching components by enumerating.</summary>
        public int Count()
        {
            int n = 0;
            using var e = GetEnumerator();
            while (e.MoveNext()) n++;
            return n;
        }

        /// <summary>Returns the first matching component, or <c>null</c> if none match.</summary>
        public T First()
        {
            using var e = GetEnumerator();
            return e.MoveNext() ? e.Current : default;
        }

        /// <summary>Gets the first matching component; returns false (and <c>null</c>) if none match.</summary>
        public bool TryFirst(out T result)
        {
            using var e = GetEnumerator();
            if (e.MoveNext())
            {
                result = e.Current;
                return true;
            }
            result = null;
            return false;
        }

        /// <summary>Invokes <paramref name="action"/> for each matching component.</summary>
        public void ForEach(Action<T> action)
        {
            if (action == null) return;
            using var e = GetEnumerator();
            while (e.MoveNext()) action(e.Current);
        }

        /// <summary>Materializes all matching components into a new list.</summary>
        public List<T> ToList()
        {
            var list = new List<T>();
            using var e = GetEnumerator();
            while (e.MoveNext()) list.Add(e.Current);
            return list;
        }

        /// <summary>Allocation-free enumerator that guards the world iteration scope; dispose to release it (handled by <c>foreach</c>).</summary>
        public struct Enumerator : IDisposable
        {
            readonly IReadOnlyWorld _world;
            readonly Storage<T> _storage;
            readonly QueryFilter _filter;
            readonly int _count;
            int _index;

            bool _isDisposed;

            internal Enumerator(IReadOnlyWorld world, Storage<T> storage, QueryFilter filter)
            {
                _isDisposed = false;
                
                _world = world;
                _storage = storage;
                _filter = filter;
                _count = storage.Count;
                _index = -1;
                
                _world.BeginIterationInternal();
            }

            /// <summary>The current matching component.</summary>
            public T Current => _storage.At(_index);

            /// <summary>Advances to the next ready, filter-matching component; false when exhausted.</summary>
            public bool MoveNext()
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(Enumerator));
                while (++_index < _count)
                {
                    if (!_storage.IsReady(_index)) continue;
                    if (_filter.Matches(_storage.GetOwner(_index))) return true;
                }
                return false;
            }

            /// <summary>Ends the world iteration scope; safe to call once.</summary>
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
