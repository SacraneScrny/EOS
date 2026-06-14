# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & tooling

Pure C# class library — no `.csproj`, no solution file, no test runner. There are no build or lint commands to run. Validate changes by reading the code carefully; compilation errors only surface when the consumer project builds. The library has no external dependencies and no `UnityEngine` references — engine integration lives in a separate consumer assembly (EOS.Unity) that plugs in through the seams below. The core and the Unity bridge are compiled into the same Unity assembly (`Assembly-CSharp`, no asmdefs), so `internal` members of the core are visible to the bridge — keep that in mind before widening any `internal` API to `public`.

## Code style

- No comments except XML doc `<summary>` on the public API. Every public type and member (and the `protected` virtual/abstract override surface of public base classes) carries a brief `<summary>` — one or two lines — saying what it is and how to use it correctly, so the surface is self-documenting and you needn't reach for the README. `<see cref="..."/>` / `<c>...</c>` inline tags are allowed inside it. **No** inline `//`, block comments, or trailing comments anywhere, and nothing beyond `<summary>` on non-public members.
- No aligning tabs. Only standard indentation (4 spaces).
- All error handling via `try/catch` + `EosLog.Error/Warning` — never swallow silently. Invalid operations (boot-less updates, mid-iteration registration) log an error and bail rather than throwing, except the structural-change guard which throws by policy.
- Always pass `nameof(TheClass)` as the `source` argument to `EosLog` calls.

## Architecture

EOS is a single-threaded ECS (Entity-Component-System) framework designed for miss-cache access patterns (components in dense arrays, iterated linearly).

### Entry points

`Universe` is the static root. It owns a default `World` and a list of additional worlds. Call `Universe.Boot()` once, then drive the worlds each frame:

- `Universe.Update / FixedUpdate / LateUpdate(dt)` — fan out to every non-manual world's matching phase. Each guards with `IsBooted` (error log) and `IsEnabled` (silent no-op), and sets `Universe.IsIterating` for the duration.
- `Universe.Tick(realDelta, fixedStep = 1/60, maxSteps = 8)` — fixed-step accumulator. It calls **`Update`** (not `FixedUpdate`) zero or more times per real frame, clamping at `maxSteps`; if the backlog still exceeds one step afterwards the accumulator is zeroed (drops the remainder). `fixedStep <= 0` degenerates to a single `Update(realDelta)`.
- `Universe.DebugDraw()` — gizmo pass (see Debug draw).
- `Universe.On / Off()` toggle the whole tick; `Universe.Reset()` resets every world; `Universe.Shutdown()` disposes everything and clears `IsBooted`; `Universe.CreateWorld(key = null, isSerializable = true) / DestroyWorld / TryGetWorld` manage extra worlds (keys must be unique; the default world cannot be destroyed).
- **`Universe.Boot()` auto-loads**: after init it invokes `WorldLoader.OnLoad` and, if a snapshot comes back, calls `WorldSerializer.Restore` on it.
- World management (create/destroy/reset/on/off) is rejected with an error log while `IsIterating`.

A world with `IsManualUpdate = true` is skipped by `Universe.*` and must be driven directly.

`World` owns all subsystems and wires them together. Everything that needs `World` extends `WorldBound` and gets the reference via `Init(World)` / `OnInited()`. `Universe.DefaultWorld` is exposed as `IReadOnlyWorld`; the writable instance is `internal` (`InternalDefaultWorld`).

### Frame loop

`World.Update(dt)`:

```
CurrentPhase = Update; NextFrame
→ BeforeAll.Execute → BeforeUpdate.Execute      (ECBs — deferred structural changes applied here)
→ Events.Promote + Events.Trim                  (stage → live, retire consumed/aged)
→ InitializeSystems.Run                         (Awake + Start pending objects, then MarkReady)
┌─ iteration guard ─────────────────────────────
│ → Systems.UpdateEvents                        (EventExecute methods)
│ → Systems.Update                              (Execute methods)
│ → Objects.Update                              (IObjectUpdate components)
└───────────────────────────────────────────────
→ AfterUpdate.Execute
```

`FixedUpdate` and `LateUpdate` follow the same shape — including their own `Events.Promote/Trim` — but **skip `InitializeSystems`**. Phase asymmetry to keep in mind:

- `BeforeAll` runs **only** at the start of `Update`.
- `AfterAll` runs **only** at the end of `LateUpdate`.
- `FixedUpdate` runs neither `BeforeAll` nor `AfterAll`.

So a world driven only through `FixedUpdate` never sees those buffers (and never initializes new components), and `Tick` routes its fixed step into `Update` — the `FixedUpdate` phase only runs if you call it yourself.

Only `Systems.*` and `Objects.*` run inside the iteration guard. The `Before*/After*` ECBs and `InitializeSystems` run outside it, so structural changes there are unguarded by design. The guard is a depth counter (`_iterationDepth`), surfaced as `World.IsIterating`.

`World.Frame` advances once per phase call. `World.Version` is a separate watermark that advances only on `MarkReady` and `Bump` (see Storage); reactive cursors compare against it. `World.CurrentPhase` records the running phase and `World.AfterCurrentPhase` maps it to `AfterUpdate` / `AfterFixedUpdate` / `AfterLateUpdate` — use it for deferral when the calling phase is unknown.

### Entity identity

`EosEntity` is an immutable struct: `(int Id, ushort Version, World)`. `Name` is a property resolved from the world, not a stored field. Version increments on destroy, making stale handles detectable. `EosEntity.Null` has `Id = -1`, `Version = 0`, `World = null`. Equality compares id + version + world id.

Entities live in `EntitiesContainer` (alive list + free stack for ID reuse, sparse `_exists` / `_versions` arrays). Validity is `_exists[id] && _versions[id] == entity.Version`. `All()` returns an alloc-free struct enumerator over alive entities.

**Entities are created inactive by default.** The public constructor is `EosEntity(World world, string name = "", bool active = false, bool isSerializable = true)` (blank names normalize to `"Entity"`). `InitializeSystems` skips Awake/Start for components on an inactive entity — they sit waiting until the entity is activated. Activate with `entity.On()` (or `World.Entities.SetActive`). Entities created through an `EntityCommandBuffer.Create(...)` are constructed with `active: true`. Toggling active re-runs `RefreshReady` across the entity's components (propagated through the hierarchy, see below).

**Active state is hierarchical** (Unity semantics): `IsActive` is the *effective* state — the entity's own flag AND every ancestor's. `IsActiveSelf` is the own flag alone (what `SetActive` writes, what serialization captures). `EosObject.IsEnabled`, `InitializeSystems`, queries and per-object updates all gate on the effective `IsActive`, so deactivating a parent silently suspends the whole subtree; effective-state flips refresh readiness only for the entities that actually changed.

Stable keys (`SetStableKey` / `TryFind` / `GetStableKey`) give an entity a serialization-stable string handle independent of its runtime id.

### Hierarchy (parent-child)

`World.Hierarchy` (`HierarchyContainer`, namespace `EOS.Hierarchy`) is a per-world parent→children graph, stored like `TagsContainer`: sparse arrays indexed by entity id (`_parents` / `_firstChild` / `_nextSibling` / `_prevSibling` intrusive linked list + `_childCount` + `_branchActive` effective-active cache + `_root` cached-root). Reads are alloc-free and O(1) — `GetRoot` is a direct `_root[id]` lookup, not an ancestor walk. The only O(subtree) work is the reparent propagation: `SetParent`/`Detach` push the new root (and the effective-active flip) down the moved subtree, the same way `_branchActive` propagates.

The entity-facing surface lives in `EOS.Extensions.HierarchyExtensions`: `SetParent(parent)` / `Detach()` (or `SetParent` with `EosEntity.Null`), `GetParent` / `HasParent` / `GetRoot`, `ChildCount` / `Children()` (struct enumerator) / `GetChildren(list, recursive)` (BFS), `IsChildOf` / `IsDescendantOf`, `DetachChildren()`, `CreateChild(name, active = false, isSerializable = true)`. The ECB fluent API mirrors `SetParent(EosEntity | DeferredEntity)` and `Detach()` for deferred use from systems.

Semantics:

- **Reparenting is not a structural change** (same class as `SetActive`/tags) — allowed mid-iteration; every link change emits a `ParentChanged { Child, OldParent, NewParent }` struct event.
- **Destroy cascades**: `entity.Destroy()` destroys the whole subtree, children first (`HierarchyContainer.OnEntityDestroying` drives child `Destroy` before the parent's own teardown, so child components dispose while the parent is still alive). Call `DetachChildren()` first to spare them. The cascade goes through normal `Destroy`, so `StructuralChangePolicy` applies as usual.
- Cycles are rejected at `SetParent` (error log), as are self-parenting, cross-world links and stale handles. Links are cleaned eagerly on destroy, so stored ids always refer to alive entities. Child order is unspecified.
- Detach test is `parent.World == null` (covers both `EosEntity.Null` and `default` — `default(EosEntity)` has `Id = 0`, so never compare against `Null` by id).
- Serialization: `EntityRecord.ParentLocalId` (default `-1`) round-trips links; `Restore` relinks in a second pass after all entities exist, so record order doesn't matter. `record.Active` stores `IsActiveSelf` — effective state is recomputed from the restored links.

### Components

`EosObject` is the base class for all components. Lifecycle:

1. `Storage<T>.Add(entity)` — allocates, calls `SetupObject` → `RegisterObject` in `ObjectsContainer` (object enters the *waiting* pool).
2. `InitializeSystems.Run()` runs each `Update` before systems — for each waiting object on a valid, active entity it calls `Awake()` then `Start()`, then `MarkReady` (signals the `[New]` reactive channel) and moves it to the *inited* pool. An exception inside `OnAwake`/`OnStart` sets `IsFailed` and the object never becomes ready.
3. Per-frame update via the `IObjectUpdate / IObjectFixedUpdate / IObjectLateUpdate` interfaces (methods `OnUpdate / OnFixedUpdate / OnLateUpdate(float deltaTime)`; each interface also requires `bool IsEnabled { get; }`, which `EosObject` already provides).
4. `Dispose()` — called by `Storage.Remove` or `Storage.Clear`; runs traced disposables (`Trace(IDisposable)` / `Trace(params IDisposable[])`), then `OnDispose`, then unregisters.

Protected virtuals: `OnAwake`, `OnStart`, `OnDispose`, `OnDebugDraw`. Protected helpers operating on the owning entity: `Add<T> / Get<T> / TryGet<T> / Has<T> / Remove<T>`, plus `Services` (the world's locator) and `Bump()`.

`IsEnabled` = `IsAwaken && IsStarted && enabled && Entity.IsActive`; only ready, enabled objects are visited by queries and per-object updates. `SetEnabled(bool)` / `Enable()` / `Disable()` flip the private flag and refresh readiness. Call `Bump()` inside a component (or `entity.Bump<T>()` outside) to signal the `[Bumped]` reactive channel (deduped to once per frame).

### Storage

`Storage<T>` is a dense-array sparse-set, not a dictionary. Parallel arrays (`_data`, `_owners`, `_ownerVersions`, `_addVersion`, `_markVersion`, `_ready`, …) are indexed by a dense slot; `_sparse` maps `entity.Id → dense slot`, validated by `_owners[slot] == id && _ownerVersions[slot] == version`. Removal uses swap-remove to keep the dense array contiguous, fixing up `_sparse` for the moved tail element. `All` exposes the dense span; `At(index)` / `GetOwner(index)` / `IsReady(index)` are the iteration primitives queries build on.

Reactive watermarks live here: `MarkReady` stamps `_addVersion` (drives `[New]`), `Bump` stamps `_markVersion` (drives `[Bumped]`), both via `World.NextVersion()`. `MaxAddVersion` / `MaxMarkVersion` are monotonic per-storage high-water marks used to early-out reactive scans.

**Component pooling** is opt-in per type via the empty marker `IPoolableObject` (`EOS.Objects.Interfaces`). When `T : IPoolableObject`, `Storage<T>` keeps an unbounded `Stack<T>`: `Remove` disposes the instance as usual (so `OnDispose` still runs), then `EosObject.ResetForReuse()` restores its lifecycle flags to construction defaults and returns it to the pool instead of dropping it on GC; `Add` rents from the pool when non-empty instead of `new T()`. The reused instance re-runs the full lifecycle (`Awake`/`Start` → `MarkReady`), so `[New]` re-fires and pooling is transparent — there are **no** `OnRent`/`OnReturn` hooks (unlike Unity view pooling, where Unity doesn't re-call `Awake`/`OnDestroy`); reset stale data fields in `OnDispose` or fully initialize in `OnAwake`. Types without the marker always allocate a fresh instance. `Clear()` drains the pool.

`ObjectsStorageMap` is the registry: `Get<T>()` lazily creates and caches `Storage<T>`, also indexing it under every interface it implements (for interface-driven queries) and per owning entity (for fast destroy/refresh). Non-generic access: `GetOrCreate(Type)` (used by preset/deserialization paths), `GetConcrete(Type)`, `GetByInterface(Type)`. **Do not call `_map.Clear()` or `_byInterface.Clear()` in `Reset()`** — system closures and generated bodies hold direct references to Storage instances; replacing them breaks all existing queries. `Reset()` clears each storage's contents but keeps the instances.

### Systems

`EosSystem` subclasses are discovered at `World.Init()` (every non-abstract subclass, `Activator.CreateInstance`). Declare an `Execute(...)` method — its parameter types define the query:

| Parameter | Meaning |
|-----------|---------|
| `T : EosObject` | mandatory concrete component |
| `[Optional] T` | optional concrete component |
| `IFoo` | mandatory interface component (fan-out across all implementations) |
| `[Each] IFoo` | cartesian fan-out over all matching implementations |
| `[New] T` | reactive: fires only when T was recently added (`MarkReady`) |
| `[Bumped] T` | reactive: fires only when `Bump()` was called on T this version window |
| `EosEntity` | receives the owning entity |
| `float` | receives delta time |

Method-level filters: `[Include(params Type[])]` / `[Exclude(params Type[])]` (has / not-has), and tag filters `[WithTag]`, `[WithoutTag]`, `[WithAnyTag]`, `[WithOneTag]` (all `params object[]` — strings or enum values).

`[Group(typeof(MyGroup))]` on the class assigns it to a `SystemGroup`. Groups nest by inheritance and support hierarchical enable/disable (`World.SystemGroups.Enable<T>()` / `Disable<T>()` / `SetEnabled(Type, bool)` / `IsEnabled(Type)`); a group counts as enabled only if it and all ancestors are enabled.

Ordering within the same group level is a topological sort: `[UpdateAfter(typeof(Other))]` / `[UpdateBefore(...)]` are edges; `[UpdateOrder(int)]` (or `UpdateOrderPhase.BeforeAll / AfterAll`, which are `int.MinValue / MaxValue`) is the tie-break priority, then type name, then discovery index — fully deterministic, cycles throw.

System surface: the overridable `UpdateType` property routes a whole system to the `Update`, `FixedUpdate`, or `LateUpdate` phase. `UpdateWhen()` (protected virtual) and `IsEnabled` gate it at runtime; `On()` / `Off()` toggle `IsEnabled` with `OnEnable` / `OnDisable` callbacks; `Awake()` / `Start()` are public one-time lifecycle virtuals; `Context` returns a `LocalSystemContext`, `Services` the world locator.

Reactive systems track a `Cursor` (world version watermark). **The cursor advances every frame even when the system's group is disabled**, so events that occur during a disabled period are dropped when the group re-enables. New reactive systems start with their cursor at the current version, so they don't fire for components that already existed.

### System execution: reflection vs codegen

Two interchangeable paths produce identical results:

- **Reflection (default).** `World.Init()` reflects over every non-abstract `EosSystem`, builds a delegate per `Execute` / `EventExecute` from the parameter shape, and invokes the body. Correct and zero-config, but it builds `object[]` argument arrays and boxes per match.
- **Codegen (opt-in, zero-alloc).** `SystemRegistryGenerator.Generate(outputDirectory = "_Generated", @namespace = "EOS.Generated", className = "EosGeneratedSystems", fileName = "EosGeneratedSystems.g.cs")` emits a registry that registers itself through a `[ModuleInitializer]` into `GeneratedSystems.Provider`. When a provider is present, `SystemsRunner` uses generated factories and typed, allocation-free bodies, and calls `PreserveStorages` first so every component `Storage<T>` exists before bodies bind to it. Re-run the generator after adding, removing, or changing systems. Any method whose shape can't be typed (or a stale registry) falls back to reflection per-method with a warning — nothing breaks, it just deoptimizes.

`SystemShape` / `SystemSignature` are the shared shape-and-identity model both paths agree on. **Open-generic systems are unsupported** by either path: discovery can't `Activator.CreateInstance` an open generic, and there is no explicit per-type registration hook. A system that needs a concrete `T` in its body must be a closed, named subclass (`class FooSystem : BarSystem<Baz>`).

### Imperative queries (external access)

`World.Query<...>()` (extension on `IReadOnlyWorld`, namespace `EOS.Queries`) is the imperative counterpart to system `Execute` queries, for code that runs **outside** the system loop — UI, MonoBehaviours, editor tools. It returns a `readonly struct EntityQuery<T>` / `EntityQuery<T1,T2>` / `EntityQuery<T1,T2,T3>` with an allocation-free struct enumerator:

```csharp
foreach (var health in world.Query<Health>()) { ... }
foreach (var (pos, vel) in world.Query<Position, Velocity>()) { ... }
```

It visits only **ready, enabled** components (same `IsReady` gate as systems), pivots on the smallest storage for multi-component queries, and dedups by entity. Fluent filters mirror the system attributes: `.With<T>()` / `.Without<T>()` (has / not-has, both ready-gated), `.WithTag / .WithoutTag / .WithAnyTag / .WithOneTag(...)`. Each fluent call returns a new immutable query struct (copy-on-write filter arrays — small allocs at configuration time only; enumeration stays alloc-free). Terminal helpers: `Any()`, `Count()`, `First()` / `TryFirst(out)`, `ForEach(...)`, `ToList()`. Multi-component results are `QueryResult<...>` structs (`.Entity`, `.Item1…`, with `Deconstruct`). Reactive `[New]`/`[Bumped]` channels are system-only and intentionally not exposed here. Enumeration is read-only; structural changes during it are caught by `StructuralChangePolicy` just like systems.

### Generic components

`EosObject` subclasses may be generic. A **closed** generic (`Incarnation<Transform>`) is an ordinary concrete component: `Storage<T>` keys by `typeof(T)`, so each closed type gets its own dense array and is still indexed under every interface it implements.

Query a generic component one of two ways:

- **By closed type** — `Execute(Incarnation<Transform> inc)` behaves like any concrete parameter.
- **By a non-generic interface it implements** — declare `interface IFoo` on the generic class, then `Execute([Each] IFoo foo)` fans out across every closed type. `[Each]` is required here: the interface query dedups by entity, so without it an entity carrying two closed variants only fires once.

The idiom: keep the type-dependent work behind a non-generic interface and keep systems non-generic. `Incarnation<TView>` is the canonical example (see Incarnation below).

Serialization of closed generics needs no per-type registration or attributes: `WorldSerializer` stores `Type.AssemblyQualifiedName` and resolves it version-tolerantly (`ResolveType` matches assemblies by simple name, including each generic argument), so snapshots survive assembly version drift.

### Events

One-frame, read-once events modelled on the DOTS pattern (emit now, every interested system reads exactly once, then gone) — without entities or `EosObject` overhead. Events are plain **structs** flowing through per-type `EventChannel<T>`, owned by `World.Events` (`EventsContainer`).

Emit with `World.Event(in T e)` — the struct is copied into the channel's staging buffer. No closures, no entity, no structural change, so it is safe mid-iteration:

```csharp
World.Event(new DamageEvent { Target = e, Amount = 5 });
```

Read with an `EventExecute(T e)` method on an `EosSystem` (optionally `EventExecute(T e, float dt)`). A system may declare several for different event types. `EventExecute` runs **before** `Execute` in the same phase and is ordered through the same `[Group]` / `[UpdateBefore]` / `[UpdateAfter]` / `[UpdateOrder]` graph (sorted independently over the event list).

```csharp
class DamageSystem : EosSystem
{
    void EventExecute(DamageEvent e) { /* fires once per event */ }
    void Execute(Health h) { /* normal query, runs after events */ }
}
```

Lifecycle each phase: `Promote` moves staging → live with ascending sequence numbers (events surface one tick after emit); `Trim` drops live events. **Read-once is guaranteed by a per-`EventExecute` cursor** (a sequence watermark, like `[New]`), so multi-tick `FixedUpdate` and phase ordering never double-read. Retirement is **min-cursor**: an event lives until every registered consumer of its type has advanced past it, then it is dropped. `EventsContainer.MaxAge` (const 16 frames) is a hard cap so an undriven phase can't leak. A consumer in a disabled group still advances its cursor (events are dropped for it), consistent with the reactive caveat above.

**Do not clear the `_channels` map in `Reset()`** — system closures hold direct channel references and consumer slots; `Reset()` only clears each channel's buffers and cursors.

### Deferred structural changes

`World.BeforeAll / BeforeUpdate / AfterUpdate / BeforeFixedUpdate / AfterFixedUpdate / BeforeLateUpdate / AfterLateUpdate / AfterAll` are `EntityCommandBuffer` instances exposed as `IEntityCommandScheduler` (plus `AfterCurrentPhase`, which picks the after-buffer of the running phase). Use them to defer `Create`, `Add<T>`, `Remove<T>`, `Destroy` (and tag mutations) when calling from inside a system's `Execute`. Each buffer executes at its matching point in the frame loop above.

The fluent API (`CommandChain` / `BoundSchedule`) chains conditionals and ops: `Schedule(entity)` or `Create(name = "", isSerializable = true)` then `When<T>` / `If` / `WhenTag` / `WhenNoTag` / `WhenAnyTag` / `WhenOneTag`, `Add<T>` / `Add<T>(configure)` / `Change<T>` / `ChangeOrAdd<T>` / `Remove<T>` / `Destroy`, `SetParent(EosEntity | DeferredEntity)` / `Detach`, `AddTag` / `RemoveTag` / `SetFlag` / `ClearTags`. `Create` returns a `DeferredEntity` (`Value`, `IsResolved`) you can schedule further ops against; it resolves when the buffer runs, and the created entity is **active immediately**. Semantics: a false condition **short-circuits the rest of that chain**; `Add<T>` is add-if-missing (the configure callback only runs for a fresh add); `Change<T>` mutates only if present; `ChangeOrAdd<T>` always acts. `Execute()` drains ops scheduled during execution in the same pass, with a 10 000-op runaway guard that clears the buffer and logs an error. A standalone `CommandChain` can be reused across entities via `Schedule(entity, chain)` (ops are copied).

`StructuralChangePolicy` (default `Throw`) enforces deferral: direct structural changes during system iteration throw `InvalidOperationException`. Set to `Warn` or `Allow` to mutate in place.

### Tags

`World.Tags` (`TagsContainer`) is a per-entity bitmask. Tag keys are `string` or any `enum` value, interned to bit indices by `TagRegistry`; `[Flags]` enums expand to one bit per set flag. The bit array grows in both dimensions (entities and words). Query with `Has` / `HasAll` / `HasAny` / `HasOne` / `HasAnyIn(enumType)`, the `EntityExtensions` wrappers (`HasTag`, `HasAllTags`, `HasAnyTag`, `HasOneTag`, `HasAnyIn<TEnum>`, `AddTag`, `RemoveTag`, `SetFlag`, `ClearTags`), or the system tag-filter attributes above. `BuildMask` / `MatchAll` / `MatchNone` / `MatchAny` / `MatchOne` are the precompiled-mask primitives the query paths use. Tags serialize by descriptor (string, or enum type + value) so they survive round-trips.

### Context & services

`World.Context` (`IWorldContext`) is a typed blackboard of **struct** values: `Get` / `TryGet` / `Has` / `Set` / `Clear`. Inside a system, `Context` returns a `LocalSystemContext` that adds `Changed<T>(out value)` / `Changed<T>()` — a per-system change watermark for "did this value change since I last looked" (cursors are keyed by consumer object and cleared on world init). Only values implementing `ISerializableContext` are captured into snapshots.

`World.Services` (`IServiceLocator`: `Get` / `TryGet` / `Has`) and `World.ServiceRegistry` (`IServiceRegistry`: adds `Register` / `Unregister`) form a per-world service locator. Registration/unregistration is rejected (error log) during iteration — wire services before driving the world. `Get<T>` on a missing service logs an error and returns null; `Register` silently overwrites an existing service of the same type and rejects nulls.

`WorldBootstrap.Provider` (an `Action<World>` in `EOS.Loader`) is the per-world seeding seam: `World.Init()` and `World.Reset()` funnel every world through `WorldBootstrap.Apply(this)`, which invokes the provider if one is set (null = no-op, exceptions caught + logged). It runs for the default world, worlds created via `Universe.CreateWorld`, and any created later, and re-runs on `Reset()` so context defaults survive the reset wipe (services survive `Reset` anyway; the silent-overwrite keeps re-seeding quiet). The core never populates it — the consumer does. The Unity bridge generates a `Register(World)` body from `[EosWorldBootstrap]` methods and installs it into `Provider` via `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` (before any world boots); with no generated file the provider stays null. Unlike the other static seams it is not cleared by `EosDomainReset` — it points at a stateless dispatch method and is re-installed every play.

### Incarnation (the view seam)

`Incarnation<TView>` bridges a component to an external view object (e.g. a Unity `GameObject`). `Setup(id)` stores the view id; on `Awake` it resolves an `IIncarnationBinder<TView>` from the `IncarnationBridge` registry and instantiates the view (`Instantiate(entity, incarnationId)`); on `Dispose` it destroys it. A missing binder logs at debug level and the incarnation stays viewless. The `IncarnationSync*System`s iterate `[Each] IIncarnation` once per phase and call `Sync` / `SyncFixed` / `SyncLate`, which dispatch to the typed binder — no casting or boxing in user code. The consumer assembly registers binders via `IncarnationBridge.Register<TView>(binder)` (also `Unregister<TView>` / `Resolve<TView>` / `Reset`); the core never references the view type concretely. `Incarnation<TView>` implements `IObjectSerializable` with `DataType = string` — the id round-trips, so views re-instantiate on restore. This is the primary attachment point for a rendering/engine bridge.

### Serialization

`WorldSerializer.Capture()` walks every serializable world into a `UniverseSnapshot` of plain records (entities with names/active/stable-key/tags, context values, and per-type component bags). `Restore` is **two-pass**: it first recreates every entity (remapping ids into a table), then re-adds components and calls `IObjectSerializable.DeserializeData` with an `IDeserializeContext` (`Resolve(localId)` → live entity, plus `World`) — so cross-entity references always resolve regardless of record order. `WorldSerializer.Save()` is a convenience that calls `Capture()` and hands the result to `WorldLoader.OnSave`. Components opt into data by implementing `IObjectSerializable` (`DataType` / `SerializeData` / `DeserializeData`); component presence alone is restored even without it.

Persistence is the consumer's job through `WorldLoader.OnSave` (`Action<UniverseSnapshot>`) / `WorldLoader.OnLoad` (`Func<UniverseSnapshot>`) hooks — the snapshot is plain objects, pick your own format. `Universe.Boot()` invokes `OnLoad` automatically and restores a non-null result. `World.IsSerializable` and per-entity serializable flags exclude transient state. Type resolution is version-tolerant (assemblies matched by simple name).

### Logging

`EosLog` is a static ring-buffer (1024 entries). Attach a handler via `EosLog.OnLog` (default writes to `Console`). Use `EosLog.Debug` (`[Conditional("DEBUG")]`), `EosLog.Warning`, `EosLog.Error`; levels are `Debug / Warning / Error`. `Dump()` returns the buffered entries oldest-first. Always pass `nameof(TheClass)` as the source argument.

### Profiling

`EosProfiler` is a static facade over a swappable `IEosProfilerBackend` (`Begin(label)` / `End()`), **off by default** (`Enabled = false`, `NullProfilerBackend`, zero overhead). `World.Update / FixedUpdate / LateUpdate` and every system body are auto-instrumented via `EosProfiler.Sample(label)`, which returns a `readonly struct Scope` whose `Dispose` ends the span (balanced even if the body throws; backend exceptions are caught + logged). `AggregatedProfilerBackend` accumulates ticks/calls per label; `Dump(reset = true)` returns a formatted report sorted by total time. A Unity bridge plugs its own backend in (`Begin → ProfilerMarker`, `End → End`) without touching the core.

### Debug draw

The core stays engine-free, so it owns only the dispatch hook, not a drawing API. `EosObject.OnDebugDraw()` and `EosSystem.OnDebugDraw()` are empty virtuals — override them and draw with whatever the consumer assembly has (e.g. `UnityEngine.Gizmos`). The trigger is a call site symmetric to `Universe.Update`: the bridge calls `Universe.DebugDraw()`, which fans out `Universe → World.DebugDraw → Objects.DebugDraw` (inited objects) `+ Systems.DebugDraw` (all systems), each wrapped in `try/catch + EosLog.Error` so one bad draw can't kill the pass. It runs inside the iteration guard, so accidental structural changes during a draw are caught by `StructuralChangePolicy`.

### Diagnostics

`WorldDebug` (namespace `EOS.Diagnostics`) renders text dumps: `DumpUniverse()`, `DumpWorld(IReadOnlyWorld)`, `DumpEntity(EosEntity)`. The Unity World Inspector's "Copy dump" uses it.

### Static lifecycle (engine integration)

Static state that outlives a `World` — `EosLog.OnLog`, `WorldLoader` hooks, `IncarnationBridge` binders — is reset by `EosDomainReset.Reset()`, which also calls `Universe.Shutdown()`. Call it on domain reload (and before re-`Boot`) so handlers and registrations don't leak across sessions. `Universe.Boot()` itself disposes the previous worlds and re-initializes, so re-booting is safe. `WorldBootstrap.Provider` is intentionally not cleared here (see Context & services).
