# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & tooling

Pure C# class library — no `.csproj`, no solution file, no test runner. There are no build or lint commands to run. Validate changes by reading the code carefully; compilation errors will only surface when the consumer project builds.

## Code style

- No comments of any kind.
- No aligning tabs. Only standard indentation (4 spaces).
- All error handling via `try/catch` + `EosLog.Error/Warning` — never swallow silently.

## Architecture

EOS is a single-threaded ECS (Entity-Component-System) framework designed for miss-cache access patterns (components in dense arrays, iterated linearly).

### Entry points

`Universe` is the static root. It owns a default `World` and a list of additional worlds. Call `Universe.Boot()` once, then drive `Universe.Update/FixedUpdate/LateUpdate` each frame.

`World` owns all subsystems and wires them together. Everything that needs `World` extends `WorldBound` and gets the reference via `Init(World)` / `OnInited()`.

### Entity identity

`EosEntity` is an immutable struct: `(int Id, ushort Version, World, string Name)`. Version increments on destroy, making stale handles detectable. `EosEntity.Null` has `Id=-1` and `World=null`.

Entities live in `EntitiesContainer` (alive list + free stack for ID reuse). Validity is checked via `_exists[id] && _versions[id] == entity.Version`.

### Components

`EosObject` is the base class for all components. Lifecycle:

1. `Storage<T>.Add(entity)` — allocates, calls `SetupObject` → `RegisterObject` in `ObjectsContainer`
2. `InitializeSystemRunner.Run()` runs each frame before systems — calls `Awake()` then `Start()` on waiting objects, then `MarkReady` (signals the `[New]` reactive channel)
3. Per-frame update via `IObjectUpdate / IObjectFixedUpdate / IObjectLateUpdate` interfaces on the component class
4. `Dispose()` — called by `Storage.Remove` or `Storage.Clear`

Call `Bump()` inside a component to signal the `[Bumped]` reactive channel.

### Storage

`Storage<T>` is a dense-array sparse-set. Key lookup: `Dictionary<ulong, int> _index` where key = `(Version << 32) | Id`. Removal uses swap-remove to keep the dense array contiguous.

`ObjectsStorageMap` is the registry: `Get<T>()` lazily creates and caches `Storage<T>`, also indexes by interface for interface-driven queries. **Do not call `_map.Clear()` or `_byInterface.Clear()` in `Reset()`** — system closures hold direct references to Storage instances; clearing the map creates new instances and breaks all existing system queries.

### Systems

`EosSystem` subclasses are discovered via reflection at `World.Init()`. Declare an `Execute(...)` method — its parameter types define the query:

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

Method-level: `[Include(typeof(T))]`, `[Exclude(typeof(T))]` add has/not-has filters.

`[Group(typeof(MyGroup))]` on the system class assigns it to a `SystemGroup` subclass. Groups support hierarchical enable/disable (`World.SystemGroups.Enable<T>()`).

`[UpdateAfter(typeof(OtherSystem))]`, `[UpdateBefore(...)]` control execution order within the same group level via topological sort.

Reactive systems track a `Cursor` (world version watermark). **The cursor advances every frame even when the system's group is disabled**, so events that occur during a disabled period are silently dropped when the group re-enables.

### Generic components

`EosObject` subclasses may be generic. A **closed** generic (`Incarnation<Transform>`) is an ordinary concrete component: `Storage<T>` keys by `typeof(T)`, so each closed type gets its own dense array, and `ObjectsStorageMap` still indexes it under every interface it implements.

Query a generic component one of two ways:

- **By closed type** — `Execute(Incarnation<Transform> inc)` behaves like any concrete parameter.
- **By a non-generic interface it implements** — declare `interface IFoo` on the generic class, then `Execute([Each] IFoo foo)` fans out across every closed type. `[Each]` is required here: the interface query dedups by entity, so without it an entity carrying two closed variants only fires once.

The idiom: keep the type-dependent work in the component behind a non-generic interface, and keep systems non-generic. `Incarnation<TView>` is the canonical example — the sync systems iterate `[Each] IIncarnation` and call `inc.Sync()`, which dispatches to the typed `IIncarnationBinder<TView>` resolved from the `IncarnationBridge` registry. No casting, no boxing in user code.

**Open-generic systems are not supported.** Discovery does `Activator.CreateInstance` over every non-abstract `EosSystem`, which cannot instantiate an open generic (`FooSystem<T>`). If a system genuinely needs the concrete `T` in its own body, it must be closed and registered explicitly — there is no such hook today. You never create or serialize an *open* generic, only closed ones; `typeof(Incarnation<>)` exists solely as reflection metadata for `MakeGenericType`.

Serialization of closed generics works without per-type registration or attributes: `WorldSerializer` stores `Type.AssemblyQualifiedName` and resolves it version-tolerantly (`ResolveType` matches assemblies by simple name, including each generic argument), so snapshots survive assembly version drift.

### Deferred structural changes

`World.BeforeAll/BeforeUpdate/AfterUpdate/...` are `EntityCommandBuffer` instances. Use them to defer `Create`, `Add<T>`, `Remove<T>`, `Destroy` when calling from inside a system's `Execute` to avoid mutating storage while iterating it.

`StructuralChangePolicy` (default `Throw`) enforces this: direct structural changes during system iteration throw `InvalidOperationException`. Set to `Warn` or `Allow` if you need to mutate in-place.

### Frame loop (World.Update)

```
ClearAllRecent → BeforeAll.Execute → BeforeUpdate.Execute
→ InitializeSystemRunner.Run   (Awake + Start pending objects)
→ Systems.Update               (Execute methods, inside BeginIteration guard)
→ Objects.Update               (IObjectUpdate components)
→ AfterUpdate.Execute
```

`FixedUpdate` and `LateUpdate` follow the same shape without `InitializeSystems` and `BeforeAll/AfterAll`.

### Logging

`EosLog` is a static ring-buffer (128 entries). Attach a handler via `EosLog.OnLog`. Use `EosLog.Debug` (DEBUG-only), `EosLog.Warning`, `EosLog.Error`. Always pass `nameof(TheClass)` as the source argument.

### Profiling

`EosProfiler` mirrors the `EosLog` pattern: a static facade over a swappable `IEosProfilerBackend` (`Begin(label)` / `End()`). It is **off by default** (`EosProfiler.Enabled == false`, `Backend == NullProfilerBackend.Instance`) so it adds no overhead until explicitly enabled.

`World.Update/FixedUpdate/LateUpdate` and every system are auto-instrumented: the frame loop wraps its phases (`World.Update`, `InitializeSystems`, `Systems.Update`, `Objects.Update`, …) and `SystemsRunner.Run` wraps each system body in `EosProfiler.Sample(label)`, where `label` is the system type name. `Sample` returns a `readonly struct Scope` (no allocation) whose `Dispose` ends the span, so spans stay balanced even if the body throws.

Backends:
- `NullProfilerBackend` — no-op default.
- `AggregatedProfilerBackend` — accumulates ticks/calls per label via `Stopwatch`; `Dump(reset = true)` returns a formatted report string (route it through `EosLog` or `Console`).

To instrument manually: `using (EosProfiler.Sample("MyChunk")) { ... }` or paired `EosProfiler.Begin/End`. The Unity bridge plugs in its own backend (`Begin → ProfilerMarker/Profiler.BeginSample`, `End → End`) without touching the core.
