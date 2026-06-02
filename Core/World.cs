using System;

using EOS.Entities;
using EOS.Events;
using EOS.Logging;
using EOS.Objects;
using EOS.Profiling;
using EOS.Storage;
using EOS.Systems;
using EOS.Systems.CommandBuffer;
using EOS.Systems.Groups;
using EOS.Tags;

namespace EOS.Core
{
    public interface IReadOnlyWorld
    {
        int Id { get; }
        string Key { get; }
        ulong Version { get; }
        ulong Frame { get; }

        bool IsDisposed { get; }
        bool IsEnabled { get; }
        bool IsManualUpdate { get; }

        bool IsIterating { get; }

        EntitiesContainer Entities { get; }
        ObjectsContainer Objects { get; }
        SystemsRunner Systems { get; }

        TagsContainer Tags { get; }
        ObjectsStorageMap ObjectsStorages { get; }
        SystemGroups SystemGroups { get; }
        InitializeSystemRunner InitializeSystems { get; }

        IReadOnlyEntityCommandBuffer BeforeAll { get; }
        IReadOnlyEntityCommandBuffer BeforeUpdate { get; }
        IReadOnlyEntityCommandBuffer AfterUpdate { get; }
        IReadOnlyEntityCommandBuffer BeforeFixedUpdate { get; }
        IReadOnlyEntityCommandBuffer AfterFixedUpdate { get; }
        IReadOnlyEntityCommandBuffer BeforeLateUpdate { get; }
        IReadOnlyEntityCommandBuffer AfterLateUpdate { get; }
        IReadOnlyEntityCommandBuffer AfterAll { get; }
    }

    public class World : IDisposable, IReadOnlyWorld, IEquatable<World>
    {
        public int Id { get; private set; } = -1;
        internal void SetId(int id) => Id = id;

        public string Key { get; private set; }
        internal void SetKey(string key) => Key = key;

        public bool IsSerializable { get; set; } = true;

        public bool IsDisposed { get; private set; }
        public bool IsEnabled { get; private set; }
        public bool IsManualUpdate { get; set; }

        ulong _version;
        public ulong Version => _version;
        internal ulong NextVersion() => ++_version;

        ulong _frame;
        public ulong Frame => _frame;
        internal ulong NextFrame() => ++_frame;

        #region Structural guard
        int _iterationDepth;

        public bool IsIterating => _iterationDepth > 0;

        public StructuralChangePolicy StructuralChangePolicy { get; set; } = StructuralChangePolicy.Throw;

        void BeginIteration() => _iterationDepth++;
        void EndIteration() { if (_iterationDepth > 0) _iterationDepth--; }

        internal bool GuardStructuralChange(string operation)
        {
            if (!IsIterating) return true;
            switch (StructuralChangePolicy)
            {
                case StructuralChangePolicy.Allow:
                    return true;
                case StructuralChangePolicy.Warn:
                    EosLog.Warning(
                        $"Structural change '{operation}' performed during system iteration. " +
                        "Use an EntityCommandBuffer (e.g. World.AfterUpdate) to defer it.",
                        nameof(World));
                    return true;
                default:
                    throw new InvalidOperationException(
                        $"Structural change '{operation}' is not allowed during system iteration. " +
                        "Use an EntityCommandBuffer (e.g. World.AfterUpdate) to defer it.");
            }
        }
        #endregion

        public EntitiesContainer Entities { get; } = new();
        public ObjectsContainer Objects { get; } = new();
        public SystemsRunner Systems { get; } = new();

        public TagsContainer Tags { get; } = new();
        public ObjectsStorageMap ObjectsStorages { get; } = new();
        public SystemGroups SystemGroups { get; } = new();
        public InitializeSystemRunner InitializeSystems { get; } = new();
        public EventsContainer Events { get; } = new();

        public void Event<T>(in T e) where T : struct => Events.Enqueue(e);

        readonly WorldContext _context = new();
        public IWorldContext Context => _context;
        internal WorldContext LocalContext => _context;

        readonly ServiceContainer _services = new();
        public IServiceLocator Services => _services;
        public IServiceRegistry ServiceRegistry => _services;

        #region ECB
        EntityCommandBuffer _beforeAll;
        public IReadOnlyEntityCommandBuffer BeforeAll => _beforeAll;

        EntityCommandBuffer _beforeUpdate;
        public IReadOnlyEntityCommandBuffer BeforeUpdate => _beforeUpdate;

        EntityCommandBuffer _afterUpdate;
        public IReadOnlyEntityCommandBuffer AfterUpdate => _afterUpdate;

        EntityCommandBuffer _beforeFixedUpdate;
        public IReadOnlyEntityCommandBuffer BeforeFixedUpdate => _beforeFixedUpdate;

        EntityCommandBuffer _afterFixedUpdate;
        public IReadOnlyEntityCommandBuffer AfterFixedUpdate => _afterFixedUpdate;

        EntityCommandBuffer _beforeLateUpdate;
        public IReadOnlyEntityCommandBuffer BeforeLateUpdate => _beforeLateUpdate;

        EntityCommandBuffer _afterLateUpdate;
        public IReadOnlyEntityCommandBuffer AfterLateUpdate => _afterLateUpdate;

        EntityCommandBuffer _afterAll;
        public IReadOnlyEntityCommandBuffer AfterAll => _afterAll;
        #endregion

        public void Reset()
        {
            if (IsDisposed) return;
            IsEnabled = false;
            _beforeAll.Clear();
            _beforeUpdate.Clear();
            _afterUpdate.Clear();
            _beforeFixedUpdate.Clear();
            _afterFixedUpdate.Clear();
            _beforeLateUpdate.Clear();
            _afterLateUpdate.Clear();
            _afterAll.Clear();

            ObjectsStorages.Reset();
            Tags.Reset();
            SystemGroups.Reset();
            Entities.Reset();
            Objects.Reset();
            Events.Reset();
            _context.Reset();
        }
        public void Init()
        {
            if (IsDisposed) return;
            _beforeAll = new(this);
            _beforeUpdate = new(this);
            _afterUpdate = new(this);
            _beforeFixedUpdate = new(this);
            _afterFixedUpdate = new(this);
            _beforeLateUpdate = new(this);
            _afterLateUpdate = new(this);
            _afterAll = new(this);

            ObjectsStorages.Init(this);
            Tags.Init(this);
            SystemGroups.Init(this);
            InitializeSystems.Init(this);
            Objects.Init(this);
            Entities.Init(this);
            Events.Init(this);
            Systems.Init(this);
            _context.Init(this);
            _services.Init(this);

            IsEnabled = true;
        }

        public void Update(float deltaTime)
        {
            if (IsDisposed) return;
            if (!IsEnabled) return;
            NextFrame();
            using (EosProfiler.Sample("World.Update"))
            {
                _beforeAll.Execute();
                _beforeUpdate.Execute();
                Events.Promote();
                Events.Trim();
                using (EosProfiler.Sample("InitializeSystems"))
                    InitializeSystems.Run();
                BeginIteration();
                try
                {
                    using (EosProfiler.Sample("Systems.UpdateEvents"))
                        Systems.UpdateEvents(deltaTime);
                    using (EosProfiler.Sample("Systems.Update"))
                        Systems.Update(deltaTime);
                    using (EosProfiler.Sample("Objects.Update"))
                        Objects.Update(deltaTime);
                }
                finally { EndIteration(); }
                _afterUpdate.Execute();
            }
        }
        public void FixedUpdate(float deltaTime)
        {
            if (IsDisposed) return;
            if (!IsEnabled) return;
            NextFrame();
            using (EosProfiler.Sample("World.FixedUpdate"))
            {
                _beforeFixedUpdate.Execute();
                Events.Promote();
                Events.Trim();
                BeginIteration();
                try
                {
                    using (EosProfiler.Sample("Systems.FixedUpdateEvents"))
                        Systems.FixedUpdateEvents(deltaTime);
                    using (EosProfiler.Sample("Systems.FixedUpdate"))
                        Systems.FixedUpdate(deltaTime);
                    using (EosProfiler.Sample("Objects.FixedUpdate"))
                        Objects.FixedUpdate(deltaTime);
                }
                finally { EndIteration(); }
                _afterFixedUpdate.Execute();
            }
        }
        public void LateUpdate(float deltaTime)
        {
            if (IsDisposed) return;
            if (!IsEnabled) return;
            NextFrame();
            using (EosProfiler.Sample("World.LateUpdate"))
            {
                _beforeLateUpdate.Execute();
                Events.Promote();
                Events.Trim();
                BeginIteration();
                try
                {
                    using (EosProfiler.Sample("Systems.LateUpdateEvents"))
                        Systems.LateUpdateEvents(deltaTime);
                    using (EosProfiler.Sample("Systems.LateUpdate"))
                        Systems.LateUpdate(deltaTime);
                    using (EosProfiler.Sample("Objects.LateUpdate"))
                        Objects.LateUpdate(deltaTime);
                }
                finally { EndIteration(); }
                _afterLateUpdate.Execute();
                _afterAll.Execute();
            }
        }

        public void DebugDraw()
        {
            if (IsDisposed) return;
            if (!IsEnabled) return;
            BeginIteration();
            try
            {
                Objects.DebugDraw();
                Systems.DebugDraw();
            }
            finally { EndIteration(); }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            ObjectsStorages.Reset();
            Tags.Reset();
            Entities.Reset();
            Objects.Reset();
            SystemGroups.Reset();
            _context.Reset();
            _services.Clear();
        }

        public bool Equals(World other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((World)obj);
        }
        public override int GetHashCode() => Id;
    }
}
