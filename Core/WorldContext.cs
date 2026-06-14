using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using EOS.Logging;
using EOS.Serialization;

namespace EOS.Core
{
    /// <summary>A per-world typed blackboard of struct values, keyed by type; values implementing <c>ISerializableContext</c> are captured into snapshots.</summary>
    public interface IWorldContext
    {
        /// <summary>Returns the stored value of type <typeparamref name="T"/>, or <c>default</c> (with a warning) if none is set.</summary>
        T Get<T>() where T : struct;
        /// <summary>Returns the stored value of type <typeparamref name="T"/> if present, without warning on a miss.</summary>
        bool TryGet<T>(out T value) where T : struct;
        /// <summary>Whether a value of type <typeparamref name="T"/> is currently set.</summary>
        bool Has<T>() where T : struct;
        /// <summary>Stores (or overwrites) the value of type <typeparamref name="T"/>, stamping it as changed.</summary>
        void Set<T>(in T value) where T : struct;
        /// <summary>Removes the value of type <typeparamref name="T"/>, stamping it as changed.</summary>
        void Clear<T>() where T : struct;
    }

    /// <summary>The default <see cref="IWorldContext"/> implementation backing <c>World.Context</c>; also tracks per-consumer change cursors for system-side <c>Changed</c> queries.</summary>
    public class WorldContext : WorldBound, IWorldContext
    {
        sealed class RefComparer : IEqualityComparer<object>
        {
            public static readonly RefComparer Instance = new();
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        interface ICell
        {
            bool Has { get; }
            object BoxedValue { get; }
            void ClearCursors();
        }

        sealed class Cell<T> : ICell where T : struct
        {
            public T Value;
            public bool Has;
            public ulong Stamp;
            public readonly Dictionary<object, ulong> Cursors = new(RefComparer.Instance);

            bool ICell.Has => Has;
            object ICell.BoxedValue => Value;
            void ICell.ClearCursors() => Cursors.Clear();
        }

        static readonly MethodInfo _setGenericBoxed = typeof(WorldContext)
            .GetMethod(nameof(SetGenericBoxed), BindingFlags.Instance | BindingFlags.NonPublic);

        readonly Dictionary<Type, ICell> _cells = new();
        ulong _seq;

        protected override void OnInited()
        {
            foreach (var cell in _cells.Values)
                cell.ClearCursors();
        }

        Cell<T> CellOf<T>(bool create) where T : struct
        {
            if (_cells.TryGetValue(typeof(T), out var existing))
                return (Cell<T>)existing;
            if (!create) return null;
            var created = new Cell<T>();
            _cells.Add(typeof(T), created);
            return created;
        }

        /// <summary>Stores (or overwrites) the value of type <typeparamref name="T"/>, stamping it as changed.</summary>
        public void Set<T>(in T value) where T : struct
        {
            var cell = CellOf<T>(true);
            cell.Value = value;
            cell.Has = true;
            cell.Stamp = ++_seq;
        }

        /// <summary>Returns the stored value of type <typeparamref name="T"/>, or <c>default</c> (with a warning) if none is set.</summary>
        public T Get<T>() where T : struct
        {
            var cell = CellOf<T>(false);
            if (cell == null || !cell.Has)
            {
                EosLog.Warning($"No value of type {typeof(T).Name} in context.", nameof(WorldContext));
                return default;
            }
            return cell.Value;
        }

        /// <summary>Returns the stored value of type <typeparamref name="T"/> if present, without warning on a miss.</summary>
        public bool TryGet<T>(out T value) where T : struct
        {
            var cell = CellOf<T>(false);
            if (cell == null || !cell.Has)
            {
                value = default;
                return false;
            }
            value = cell.Value;
            return true;
        }

        /// <summary>Whether a value of type <typeparamref name="T"/> is currently set.</summary>
        public bool Has<T>() where T : struct
        {
            var cell = CellOf<T>(false);
            return cell != null && cell.Has;
        }

        /// <summary>Removes the value of type <typeparamref name="T"/>, stamping it as changed.</summary>
        public void Clear<T>() where T : struct
        {
            var cell = CellOf<T>(false);
            if (cell == null || !cell.Has) return;
            cell.Has = false;
            cell.Value = default;
            cell.Stamp = ++_seq;
        }

        internal bool Changed<T>(object consumer, out T value) where T : struct
        {
            if (consumer == null)
            {
                EosLog.Warning("Change tracking requested with a null consumer.", nameof(WorldContext));
                value = default;
                return false;
            }
            var cell = CellOf<T>(false);
            if (cell == null || !cell.Has)
            {
                value = default;
                return false;
            }
            value = cell.Value;
            cell.Cursors.TryGetValue(consumer, out var cursor);
            if (cell.Stamp > cursor)
            {
                cell.Cursors[consumer] = cell.Stamp;
                return true;
            }
            return false;
        }

        internal bool Changed<T>(object consumer) where T : struct => Changed<T>(consumer, out _);

        internal void Capture(List<(Type type, object value)> into)
        {
            if (into == null) return;
            foreach (var kv in _cells)
            {
                if (!kv.Value.Has) continue;
                if (!typeof(ISerializableContext).IsAssignableFrom(kv.Key)) continue;
                into.Add((kv.Key, kv.Value.BoxedValue));
            }
        }

        void SetGenericBoxed<T>(object boxed) where T : struct => Set((T)boxed);

        internal void RestoreValue(Type type, object value)
        {
            if (type == null || value == null) return;
            try
            {
                _setGenericBoxed.MakeGenericMethod(type).Invoke(this, new[] { value });
            }
            catch (Exception ex)
            {
                EosLog.Error($"Failed to restore context value of type {type.Name}: {ex.Message}", nameof(WorldContext));
            }
        }

        internal void Reset()
        {
            _cells.Clear();
            _seq = 0;
        }
    }

    /// <summary>A system's view onto the world context, exposed as <c>EosSystem.Context</c>; adds per-system <see cref="Changed{T}(out T)"/> change tracking on top of the plain accessors.</summary>
    public readonly struct LocalSystemContext
    {
        readonly WorldContext _context;
        readonly object _consumer;

        internal LocalSystemContext(WorldContext context, object consumer)
        {
            _context = context;
            _consumer = consumer;
        }

        /// <summary>Stores (or overwrites) the value of type <typeparamref name="T"/>.</summary>
        public void Set<T>(in T value) where T : struct => _context.Set(value);
        /// <summary>Returns the stored value of type <typeparamref name="T"/>, or <c>default</c> (with a warning) if none is set.</summary>
        public T Get<T>() where T : struct => _context.Get<T>();
        /// <summary>Returns the stored value of type <typeparamref name="T"/> if present.</summary>
        public bool TryGet<T>(out T value) where T : struct => _context.TryGet(out value);
        /// <summary>Whether a value of type <typeparamref name="T"/> is currently set.</summary>
        public bool Has<T>() where T : struct => _context.Has<T>();
        /// <summary>Removes the value of type <typeparamref name="T"/>.</summary>
        public void Clear<T>() where T : struct => _context.Clear<T>();
        /// <summary>Returns the current value of <typeparamref name="T"/> and true if it changed since this system last observed it.</summary>
        public bool Changed<T>(out T value) where T : struct => _context.Changed<T>(_consumer, out value);
        /// <summary>Returns true if the value of <typeparamref name="T"/> changed since this system last observed it.</summary>
        public bool Changed<T>() where T : struct => _context.Changed<T>(_consumer);
    }
}
