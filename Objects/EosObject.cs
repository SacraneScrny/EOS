using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Logging;

namespace EOS.Objects
{
    /// <summary>Base class for all ECS components; lives in a <see cref="EOS.Storage.Storage{T}"/>, bound to an entity, with an Awake/Start/Dispose lifecycle.</summary>
    [Serializable]
    public abstract class EosObject : IDisposable
    {
        /// <summary>True once <c>Awake</c> has run successfully.</summary>
        public bool IsAwaken { get; private set; }
        /// <summary>True once <c>Start</c> has run successfully.</summary>
        public bool IsStarted { get; private set; }
        /// <summary>True after the component has been disposed.</summary>
        public bool IsDisposed { get; private set; }
        /// <summary>True if an exception was thrown during <c>OnAwake</c>/<c>OnStart</c>; the component never becomes ready.</summary>
        public bool IsFailed { get; private set; }
        /// <summary>True once the component has been bound to an owning entity.</summary>
        public bool HasEntity { get; private set; }
        /// <summary>Effective enabled state: awaken, started, locally enabled, and on an active entity. Only enabled objects are visited by queries and per-object updates.</summary>
        public bool IsEnabled => IsAwaken && IsStarted && _enabled && Entity.IsActive;
        /// <summary>True when this component was restored from a snapshot rather than newly created.</summary>
        public bool IsDeserialized { get; internal set; }

        bool _enabled = true;
        /// <summary>The entity this component belongs to; <see cref="EosEntity.Null"/> until bound.</summary>
        public EosEntity Entity { get; private set; } = EosEntity.Null;

        internal int UpdateIndex = -1;
        internal int FixedIndex = -1;
        internal int LateIndex = -1;
        internal int PoolIndex = -1;
        internal bool Initialized;

        internal void SetupObject(EosEntity entity)
        {
            Entity = entity;
            HasEntity = true;
            Entity.World.Objects.RegisterObject(this);
        }

        internal void ResetForReuse()
        {
            IsAwaken = false;
            IsStarted = false;
            IsDisposed = false;
            IsFailed = false;
            HasEntity = false;
            IsDeserialized = false;
            _enabled = true;
            Entity = EosEntity.Null;
            UpdateIndex = -1;
            FixedIndex = -1;
            LateIndex = -1;
            PoolIndex = -1;
            Initialized = false;
            _disposables = null;
        }

        internal void Awake()
        {
            if (!HasEntity || IsAwaken) return;
            try
            {
                OnAwake();
            }
            catch (Exception e)
            {
                EosLog.Error($"Exception in OnAwake of {this}: {e}", nameof(EosObject));
                IsFailed = true;
                return;
            }
            IsAwaken = true;
        }
        /// <summary>Override for one-time initialization run before <c>OnStart</c>; an exception here marks the component failed.</summary>
        protected virtual void OnAwake() { }

        internal void Start()
        {
            if (!IsAwaken || IsStarted) return;
            try
            {
                OnStart();
            }
            catch (Exception e)
            {
                EosLog.Error($"Exception in OnStart of {this}: {e}", nameof(EosObject));
                IsFailed = true;
                return;
            }
            IsStarted = true;
        }
        /// <summary>Override for one-time initialization run after <c>OnAwake</c>, just before the component becomes ready.</summary>
        protected virtual void OnStart() { }

        internal void DebugDraw()
        {
            if (IsDisposed) return;
            OnDebugDraw();
        }
        /// <summary>Override to draw debug gizmos during the <c>Universe.DebugDraw</c> pass using the consumer's drawing API.</summary>
        protected virtual void OnDebugDraw() { }

        List<IDisposable> _disposables;
        /// <summary>Registers a disposable to be disposed automatically when this component is disposed.</summary>
        protected void Trace(IDisposable disposable)
        {
            _disposables ??= new List<IDisposable>();
            _disposables.Add(disposable);
        }
        /// <summary>Registers multiple disposables to be disposed automatically when this component is disposed.</summary>
        protected void Trace(params IDisposable[] disposables)
        {
            _disposables ??= new List<IDisposable>();
            _disposables.AddRange(disposables);
        }

        /// <summary>Disposes the component: runs traced disposables, then <c>OnDispose</c>, then unregisters it. Idempotent.</summary>
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            try 
            {
                if (_disposables != null)
                    foreach (var d in _disposables)
                        d.Dispose();
            }
            catch (Exception e)
            {
                EosLog.Error($"Exception while disposing {this}: {e}", nameof(EosObject));
            }
            _disposables = null;
            
            try
            {
                OnDispose();
            }
            catch (Exception e)
            {
                EosLog.Error($"Exception in OnDispose of {this}: {e}", nameof(EosObject));
            }
            
            Entity.World.Objects.UnregisterObject(this);
        }
        /// <summary>Override for teardown; reset stale data fields here for pooled components, since reused instances re-run the full lifecycle.</summary>
        protected virtual void OnDispose() { }

        /// <summary>Adds (or returns the existing) component of type <typeparamref name="T"/> on the owning entity.</summary>
        protected T Add<T>() where T : EosObject, new() => Entity.Add<T>();
        /// <summary>Gets the component of type <typeparamref name="T"/> on the owning entity.</summary>
        protected T Get<T>() where T : EosObject, new() => Entity.Get<T>();
        /// <summary>Tries to get the component of type <typeparamref name="T"/> on the owning entity.</summary>
        protected bool TryGet<T>(out T result) where T : EosObject, new() => Entity.TryGet(out result);
        /// <summary>True if the owning entity has a component of type <typeparamref name="T"/>.</summary>
        protected bool Has<T>() where T : EosObject, new() => Entity.Has<T>();
        /// <summary>Removes the component of type <typeparamref name="T"/> from the owning entity.</summary>
        protected bool Remove<T>() where T : EosObject, new() => Entity.Remove<T>();

        /// <summary>The owning world's service locator.</summary>
        protected IServiceLocator Services => Entity.World.Services;

        /// <summary>Signals the <c>[Bumped]</c> reactive channel for this component (deduped to once per frame).</summary>
        protected void Bump()
        {
            if (HasEntity && Entity.IsValid)
                Entity.World.ObjectsStorages.Bump(this);
        }

        /// <summary>Sets the local enabled flag and refreshes readiness; affects whether queries and updates visit this component.</summary>
        public void SetEnabled(bool value)
        {
            if (_enabled == value) return;
            _enabled = value;
            if (HasEntity && Entity.IsValid)
                Entity.World.ObjectsStorages.RefreshReady(this);
        }
        /// <summary>Enables the component (shorthand for <c>SetEnabled(true)</c>).</summary>
        public void Enable() => SetEnabled(true);
        /// <summary>Disables the component (shorthand for <c>SetEnabled(false)</c>).</summary>
        public void Disable() => SetEnabled(false);
    }
}
