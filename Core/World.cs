using System;

using EOS.Entities;
using EOS.Events;
using EOS.Hierarchy;
using EOS.Loader;
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
    /// <summary>Read-only view of a <see cref="World"/> and its subsystems; the type external code (queries, tooling) receives.</summary>
    public interface IReadOnlyWorld
    {
        /// <summary>Unique per-process world id assigned at registration (-1 until set).</summary>
        int Id { get; }
        /// <summary>Optional unique string key for this world; null for the default world.</summary>
        string Key { get; }
        /// <summary>Reactive version watermark; advances on <c>MarkReady</c> and <c>Bump</c>, compared by reactive cursors.</summary>
        ulong Version { get; }
        /// <summary>Frame counter; advances once per phase call (Update/FixedUpdate/LateUpdate).</summary>
        ulong Frame { get; }

        /// <summary>True once the world has been disposed; all phase calls become no-ops.</summary>
        bool IsDisposed { get; }
        /// <summary>True when the world participates in the tick; phase calls no-op while false.</summary>
        bool IsEnabled { get; }
        /// <summary>When true the world is skipped by <c>Universe.*</c> and must be driven directly.</summary>
        bool IsManualUpdate { get; }

        /// <summary>True while inside the system/object iteration guard; structural changes are policed here.</summary>
        bool IsIterating { get; }

        /// <summary>The world's entity registry (alive list, id reuse, validity).</summary>
        EntitiesContainer Entities { get; }
        /// <summary>The per-object lifecycle/update container (waiting and inited pools).</summary>
        ObjectsContainer Objects { get; }
        /// <summary>The system discovery and execution runner.</summary>
        SystemsRunner Systems { get; }

        /// <summary>Per-entity tag bitmask store.</summary>
        TagsContainer Tags { get; }
        /// <summary>Per-world parent-child hierarchy graph.</summary>
        HierarchyContainer Hierarchy { get; }
        /// <summary>Registry of component <c>Storage&lt;T&gt;</c> instances, keyed by type and interface.</summary>
        ObjectsStorageMap ObjectsStorages { get; }
        /// <summary>System group tree for hierarchical enable/disable.</summary>
        SystemGroups SystemGroups { get; }
        /// <summary>Runs Awake/Start on waiting objects before systems each Update.</summary>
        InitializeSystemRunner InitializeSystems { get; }
        /// <summary>Per-type event channel store driving <c>EventExecute</c> consumers.</summary>
        EventsContainer Events { get; }

        /// <summary>Emits a struct event into its channel's staging buffer; safe mid-iteration, surfaces one tick later.</summary>
        void Event<T>(in T e) where T : struct;

        /// <summary>Typed struct blackboard for this world; <c>Get</c>/<c>TryGet</c>/<c>Has</c>/<c>Set</c>/<c>Clear</c>.</summary>
        IWorldContext Context { get; }

        /// <summary>Read-only service locator for this world (<c>Get</c>/<c>TryGet</c>/<c>Has</c>).</summary>
        IServiceLocator Services { get; }
        /// <summary>Service registry for this world; register/unregister before driving (rejected during iteration).</summary>
        IServiceRegistry ServiceRegistry { get; }

        /// <summary>Command buffer executed at the start of <c>Update</c> only.</summary>
        IEntityCommandScheduler BeforeAll { get; }
        /// <summary>Command buffer executed at the start of every <c>Update</c> phase.</summary>
        IEntityCommandScheduler BeforeUpdate { get; }
        /// <summary>Command buffer executed at the end of every <c>Update</c> phase.</summary>
        IEntityCommandScheduler AfterUpdate { get; }
        /// <summary>Command buffer executed at the start of every <c>FixedUpdate</c> phase.</summary>
        IEntityCommandScheduler BeforeFixedUpdate { get; }
        /// <summary>Command buffer executed at the end of every <c>FixedUpdate</c> phase.</summary>
        IEntityCommandScheduler AfterFixedUpdate { get; }
        /// <summary>Command buffer executed at the start of every <c>LateUpdate</c> phase.</summary>
        IEntityCommandScheduler BeforeLateUpdate { get; }
        /// <summary>Command buffer executed at the end of every <c>LateUpdate</c> phase.</summary>
        IEntityCommandScheduler AfterLateUpdate { get; }
        /// <summary>Command buffer executed at the end of <c>LateUpdate</c> only.</summary>
        IEntityCommandScheduler AfterAll { get; }

        internal void BeginIterationInternal();
        internal void EndIterationInternal();
    }

    /// <summary>Owns and wires together all subsystems for one ECS world; created and driven by <see cref="Universe"/>.</summary>
    public class World : IDisposable, IReadOnlyWorld, IEquatable<World>
    {
        /// <summary>Unique per-process world id assigned at registration (-1 until set).</summary>
        public int Id { get; private set; } = -1;
        internal void SetId(int id) => Id = id;

        /// <summary>Optional unique string key for this world; null for the default world.</summary>
        public string Key { get; private set; }
        internal void SetKey(string key) => Key = key;

        /// <summary>When true the world is captured by <see cref="WorldSerializer"/> snapshots; set false for transient worlds.</summary>
        public bool IsSerializable { get; set; } = true;

        /// <summary>True once the world has been disposed; all phase calls become no-ops.</summary>
        public bool IsDisposed { get; private set; }
        /// <summary>True when the world participates in the tick; phase calls no-op while false.</summary>
        public bool IsEnabled { get; private set; }
        /// <summary>When true the world is skipped by <c>Universe.*</c> and must be driven directly.</summary>
        public bool IsManualUpdate { get; set; }

        ulong _version;
        /// <summary>Reactive version watermark; advances on <c>MarkReady</c> and <c>Bump</c>, compared by reactive cursors.</summary>
        public ulong Version => _version;
        internal ulong NextVersion() => ++_version;

        ulong _frame;
        /// <summary>Frame counter; advances once per phase call (Update/FixedUpdate/LateUpdate).</summary>
        public ulong Frame => _frame;
        internal ulong NextFrame() => ++_frame;

        ulong _reactiveRetentionFrames = 16;
        /// <summary>Frame window that age-trimmed reactive state (<c>[Removed]</c> log, event channels) is kept; computed from the longest <c>[Delay]</c>/<c>[DelayFrame]</c> at init so a sleeping system never misses those edges. Never below 16.</summary>
        public ulong ReactiveRetentionFrames => _reactiveRetentionFrames;
        internal void SetReactiveRetentionFrames(ulong frames) => _reactiveRetentionFrames = frames < 16 ? 16 : frames;

        #region Structural guard
        int _iterationDepth;

        /// <summary>True while inside the system/object iteration guard; structural changes are policed here.</summary>
        public bool IsIterating => _iterationDepth > 0;

        /// <summary>Controls whether direct structural changes during iteration throw, warn, or are allowed; defaults to <see cref="StructuralChangePolicy.Throw"/>.</summary>
        public StructuralChangePolicy StructuralChangePolicy { get; set; } = StructuralChangePolicy.Throw;

        void IReadOnlyWorld.BeginIterationInternal() => BeginIteration();
        void IReadOnlyWorld.EndIterationInternal() => EndIteration();
        
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

        /// <summary>The world's entity registry (alive list, id reuse, validity).</summary>
        public EntitiesContainer Entities { get; } = new();
        /// <summary>The per-object lifecycle/update container (waiting and inited pools).</summary>
        public ObjectsContainer Objects { get; } = new();
        /// <summary>The system discovery and execution runner.</summary>
        public SystemsRunner Systems { get; } = new();

        /// <summary>Per-entity tag bitmask store.</summary>
        public TagsContainer Tags { get; } = new();
        /// <summary>Per-world parent-child hierarchy graph.</summary>
        public HierarchyContainer Hierarchy { get; } = new();
        /// <summary>Registry of component <c>Storage&lt;T&gt;</c> instances, keyed by type and interface.</summary>
        public ObjectsStorageMap ObjectsStorages { get; } = new();
        /// <summary>System group tree for hierarchical enable/disable.</summary>
        public SystemGroups SystemGroups { get; } = new();
        /// <summary>Runs Awake/Start on waiting objects before systems each Update.</summary>
        public InitializeSystemRunner InitializeSystems { get; } = new();
        /// <summary>Per-type event channel store driving <c>EventExecute</c> consumers.</summary>
        public EventsContainer Events { get; } = new();

        /// <summary>Emits a struct event into its channel's staging buffer; safe mid-iteration, surfaces one tick later.</summary>
        public void Event<T>(in T e) where T : struct => Events.Enqueue(e);

        readonly WorldContext _context = new();
        /// <summary>Typed struct blackboard for this world; <c>Get</c>/<c>TryGet</c>/<c>Has</c>/<c>Set</c>/<c>Clear</c>.</summary>
        public IWorldContext Context => _context;
        internal WorldContext LocalContext => _context;

        readonly ServiceContainer _services = new();
        /// <summary>Read-only service locator for this world (<c>Get</c>/<c>TryGet</c>/<c>Has</c>).</summary>
        public IServiceLocator Services => _services;
        /// <summary>Service registry for this world; register/unregister before driving (rejected during iteration).</summary>
        public IServiceRegistry ServiceRegistry => _services;

        #region ECB
        EntityCommandBuffer _beforeAll;
        /// <summary>Command buffer executed at the start of <c>Update</c> only.</summary>
        public IEntityCommandScheduler BeforeAll => _beforeAll;

        EntityCommandBuffer _beforeUpdate;
        /// <summary>Command buffer executed at the start of every <c>Update</c> phase.</summary>
        public IEntityCommandScheduler BeforeUpdate => _beforeUpdate;

        EntityCommandBuffer _afterUpdate;
        /// <summary>Command buffer executed at the end of every <c>Update</c> phase.</summary>
        public IEntityCommandScheduler AfterUpdate => _afterUpdate;

        EntityCommandBuffer _beforeFixedUpdate;
        /// <summary>Command buffer executed at the start of every <c>FixedUpdate</c> phase.</summary>
        public IEntityCommandScheduler BeforeFixedUpdate => _beforeFixedUpdate;

        EntityCommandBuffer _afterFixedUpdate;
        /// <summary>Command buffer executed at the end of every <c>FixedUpdate</c> phase.</summary>
        public IEntityCommandScheduler AfterFixedUpdate => _afterFixedUpdate;

        EntityCommandBuffer _beforeLateUpdate;
        /// <summary>Command buffer executed at the start of every <c>LateUpdate</c> phase.</summary>
        public IEntityCommandScheduler BeforeLateUpdate => _beforeLateUpdate;

        EntityCommandBuffer _afterLateUpdate;
        /// <summary>Command buffer executed at the end of every <c>LateUpdate</c> phase.</summary>
        public IEntityCommandScheduler AfterLateUpdate => _afterLateUpdate;

        EntityCommandBuffer _afterAll;
        /// <summary>Command buffer executed at the end of <c>LateUpdate</c> only.</summary>
        public IEntityCommandScheduler AfterAll => _afterAll;

        /// <summary>The phase currently running; recorded at the start of each <c>Update</c>/<c>FixedUpdate</c>/<c>LateUpdate</c>.</summary>
        public UpdateType CurrentPhase { get; private set; } = UpdateType.Update;

        /// <summary>The after-buffer matching <see cref="CurrentPhase"/>; use to defer when the calling phase is unknown.</summary>
        public IEntityCommandScheduler AfterCurrentPhase => CurrentPhase switch
        {
            UpdateType.FixedUpdate => _afterFixedUpdate,
            UpdateType.LateUpdate => _afterLateUpdate,
            _ => _afterUpdate
        };
        #endregion

        /// <summary>Clears all subsystem contents (entities, components, tags, hierarchy, events) and re-applies the bootstrap, keeping storage instances; re-enables the world.</summary>
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
            Hierarchy.Reset();
            SystemGroups.Reset();
            Entities.Reset();
            Objects.Reset();
            Events.Reset();
            _context.Reset();
            WorldBootstrap.Apply(this);
            
            EosLog.Debug($"World {Id}  has been reset.", this.ToString());
            IsEnabled = true;
        }
        /// <summary>Initializes and wires every subsystem, discovers systems, and applies the bootstrap; call once before driving the world.</summary>
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
            Hierarchy.Init(this);
            SystemGroups.Init(this);
            InitializeSystems.Init(this);
            Objects.Init(this);
            Entities.Init(this);
            Events.Init(this);
            Systems.Init(this);
            _context.Init(this);
            _services.Init(this);
            WorldBootstrap.Apply(this);

            IsEnabled = true;
        }

        /// <summary>Runs one <c>Update</c> phase: before-buffers, event promote/trim, <c>InitializeSystems</c>, then guarded systems and objects, then after-buffer.</summary>
        public void Update(float deltaTime)
        {
            if (IsDisposed) return;
            if (!IsEnabled) return;
            CurrentPhase = UpdateType.Update;
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
        /// <summary>Runs one <c>FixedUpdate</c> phase; like <see cref="Update"/> but skips <c>InitializeSystems</c> and the BeforeAll/AfterAll buffers.</summary>
        public void FixedUpdate(float deltaTime)
        {
            if (IsDisposed) return;
            if (!IsEnabled) return;
            CurrentPhase = UpdateType.FixedUpdate;
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
        /// <summary>Runs one <c>LateUpdate</c> phase; like <see cref="Update"/> but skips <c>InitializeSystems</c>, and runs the AfterAll buffer at the end.</summary>
        public void LateUpdate(float deltaTime)
        {
            if (IsDisposed) return;
            if (!IsEnabled) return;
            CurrentPhase = UpdateType.LateUpdate;
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

        /// <summary>Fans out the gizmo pass to inited objects and all systems, inside the iteration guard; invoked via <c>Universe.DebugDraw</c>.</summary>
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

        /// <summary>Tears down every subsystem and marks the world disposed; idempotent, after which all phase calls no-op.</summary>
        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            _beforeAll?.Clear();
            _beforeUpdate?.Clear();
            _afterUpdate?.Clear();
            _beforeFixedUpdate?.Clear();
            _afterFixedUpdate?.Clear();
            _beforeLateUpdate?.Clear();
            _afterLateUpdate?.Clear();
            _afterAll?.Clear();
            
            Events?.Reset();
            Systems?.Dispose();
            InitializeSystems?.Dispose();
            
            ObjectsStorages?.Reset();
            Tags?.Reset();
            Hierarchy?.Reset();
            Entities?.Reset();
            Objects?.Reset();
            SystemGroups?.Reset();
            _context?.Reset();
            _services?.Clear();
        }

        /// <summary>Two worlds are equal when they share the same <see cref="Id"/>.</summary>
        public bool Equals(World other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }
        /// <summary>Equality by <see cref="Id"/> against another <see cref="World"/>.</summary>
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((World)obj);
        }
        /// <summary>Hash code derived from <see cref="Id"/>.</summary>
        public override int GetHashCode() => Id;
    }
}
