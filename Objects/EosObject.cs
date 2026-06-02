using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Extensions;

namespace EOS.Objects
{
    public abstract class EosObject : IDisposable
    {
        public bool IsAwaken { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool HasEntity { get; private set; }
        public bool IsEnabled => IsAwaken && IsStarted && _enabled && Entity.IsActive;
        public bool IsDeserialized { get; internal set; }

        protected bool _enabled = true;
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

        internal void Awake()
        {
            if (!HasEntity || IsAwaken) return;
            IsAwaken = true;
            OnAwake();
        }
        protected virtual void OnAwake() { }

        internal void Start()
        {
            if (!IsAwaken || IsStarted) return;
            IsStarted = true;
            OnStart();
        }
        protected virtual void OnStart() { }

        internal void DebugDraw()
        {
            if (IsDisposed) return;
            OnDebugDraw();
        }
        protected virtual void OnDebugDraw() { }

        List<IDisposable> _disposables;
        protected void Trace(IDisposable disposable)
        {
            _disposables ??= new List<IDisposable>();
            _disposables.Add(disposable);
        }
        protected void Trace(params IDisposable[] disposables)
        {
            _disposables ??= new List<IDisposable>();
            _disposables.AddRange(disposables);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            if (_disposables != null)
                foreach (var d in _disposables)
                    d.Dispose();
            _disposables = null;

            OnDispose();
            Entity.World.Objects.UnregisterObject(this);
        }
        protected virtual void OnDispose() { }

        protected T Add<T>() where T : EosObject, new() => Entity.Add<T>();
        protected T Get<T>() where T : EosObject, new() => Entity.Get<T>();
        protected bool TryGet<T>(out T result) where T : EosObject, new() => Entity.TryGet(out result);
        protected bool Has<T>() where T : EosObject, new() => Entity.Has<T>();
        protected bool Remove<T>() where T : EosObject, new() => Entity.Remove<T>();

        protected IServiceLocator Services => Entity.World.Services;

        protected void Bump()
        {
            if (HasEntity && Entity.IsValid)
                Entity.World.ObjectsStorages.Bump(this);
        }
    }
}
