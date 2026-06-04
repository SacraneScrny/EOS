# EOS

**EOS** is a single-threaded **E**ntity-**C**omponent-**S**ystem framework for C#, designed for cache-friendly access patterns — components live in dense, linearly-iterated arrays. It is a pure class library with **no external dependencies** and **no engine references**: the core knows nothing about Unity, Godot, or any renderer. Engine integration plugs in through a small set of well-defined seams.

EOS borrows ideas from data-oriented designs (dense storage, deferred structural changes, one-frame events) but keeps an approachable, object-oriented authoring surface: components are classes, systems are classes, and queries are just method signatures.

---

## Highlights

- **Dense sparse-set storage.** Each component type lives in its own contiguous `Storage<T>`; iteration is linear and miss-cache friendly.
- **Signature-based queries.** A system's query is defined by the parameter types of its `Execute(...)` method — no manual archetype wiring.
- **Reactive channels.** `[New]` fires when a component is added; `[Bumped]` fires when you signal a change — both watermark-based, no per-frame scans.
- **One-frame events.** Plain structs flow through typed channels with guaranteed read-once semantics, even across multi-step fixed updates.
- **Deferred structural changes.** A fluent `EntityCommandBuffer` defers create/add/remove/destroy to safe points in the frame.
- **Tags.** Per-entity bitmask keyed by `string` or `enum`, with first-class query filters.
- **Multiple worlds.** A static `Universe` owns a default world plus any number of additional, independently-driven worlds.
- **Serialization.** Snapshot any world into plain records and restore it, version-tolerant across assembly drift.
- **Two execution paths.** Zero-config reflection by default; opt-in source-generated registry for allocation-free system dispatch.
- **Engine bridge.** The `Incarnation<TView>` seam binds a component to an external view object without the core ever naming the view type.
- **Batteries included.** Built-in logging, swappable profiling, system groups with hierarchical enable/disable, and a typed blackboard + service locator per world.

---

## Table of contents

- [Requirements & installation](#requirements--installation)
- [Quick start](#quick-start)
- [Core concepts](#core-concepts)
  - [Universe & Worlds](#universe--worlds)
  - [The frame loop](#the-frame-loop)
  - [Entities](#entities)
  - [Components](#components)
  - [Systems & queries](#systems--queries)
  - [Query filters](#query-filters)
  - [Reactive queries](#reactive-queries)
  - [Events](#events)
  - [Tags](#tags)
  - [Deferred structural changes](#deferred-structural-changes)
  - [System groups & ordering](#system-groups--ordering)
  - [Context & services](#context--services)
  - [Generic components](#generic-components)
  - [Incarnation — the view seam](#incarnation--the-view-seam)
  - [Serialization](#serialization)
  - [Reflection vs codegen](#reflection-vs-codegen)
  - [Logging, profiling & debug draw](#logging-profiling--debug-draw)
  - [Static lifecycle](#static-lifecycle)
- [Project layout](#project-layout)
- [Design notes & constraints](#design-notes--constraints)
- [License](#license)

---

## Requirements & installation

EOS is a plain C# class library — there is no `.csproj`, no solution file, and no test runner in this repository. Drop the source folders into your project (or reference them from your own `.csproj`) and you are done. The core targets a modern C# language version (records, `readonly struct`, `[ModuleInitializer]`) and has **zero NuGet dependencies**.

Everything lives under the `EOS.*` namespaces:

```csharp
using EOS.Core;        // Universe, World, services, context
using EOS.Entities;    // EosEntity
using EOS.Objects;     // EosObject, Incarnation<TView>
using EOS.Systems;     // EosSystem
using EOS.Attributes;  // [New], [Bumped], [Each], [Group], [Include], [WithTag], ...
using EOS.Extensions;  // entity helpers: Add/Get/Has/Remove, On/Off, tag helpers
```

---

## Quick start

```csharp
using System;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Systems;

// 1. A component is a class deriving from EosObject.
public class Health : EosObject
{
    public int Current;
    public int Max;
}

// 2. A system's Execute parameters ARE its query.
//    This one matches every active entity that has a Health component.
public class RegenSystem : EosSystem
{
    void Execute(Health health, float dt)
    {
        health.Current = Math.Min(health.Max, health.Current + (int)(5 * dt));
    }
}

public static class Program
{
    public static void Main()
    {
        // 3. Boot the Universe. Systems are auto-discovered at World.Init().
        Universe.Boot();

        // 4. Create a world you can populate directly.
        var world = Universe.CreateWorld("game");

        // 5. Spawn an entity, attach components, then activate it.
        var player = new EosEntity(world, "Player");
        var health = player.Add<Health>();
        health.Max = 100;
        health.Current = 40;
        player.On();

        // 6. Drive the simulation each frame.
        for (int frame = 0; frame < 10; frame++)
            Universe.Update(1f / 60f);

        Console.WriteLine(player.Get<Health>().Current); // regenerated
    }
}
```

> Entities are **created inactive by default**. `Awake`/`Start` are deferred until the entity is activated with `entity.On()`. Entities created through a command buffer are active immediately.

---

## Core concepts

### Universe & Worlds

`Universe` is the static root. It owns one default `World` plus a list of additional worlds.

```csharp
Universe.Boot();                          // create & init the default world (safe to re-boot)
var extra = Universe.CreateWorld("ui");   // additional world, returns a concrete World
Universe.TryGetWorld("ui", out var w);    // look one up by key
Universe.DestroyWorld(extra);             // dispose an additional world

Universe.Update(dt);                      // fan out Update to every non-manual world
Universe.FixedUpdate(dt);
Universe.LateUpdate(dt);
Universe.DebugDraw();                     // gizmo pass

Universe.Off();                           // pause the whole tick
Universe.On();
Universe.Reset();                         // reset every world's contents (keeps storage instances)
```

A fixed-step accumulator is provided for frame-rate-independent simulation:

```csharp
// Calls Update (not FixedUpdate) zero or more times, clamped at maxSteps to avoid the spiral of death.
Universe.Tick(realDelta, fixedStep: 1f / 60f, maxSteps: 8);
```

A world with `IsManualUpdate = true` is skipped by `Universe.*` and must be driven directly (`world.Update(dt)`).

`World` owns all subsystems and wires them together. Anything that needs a back-reference to its world extends `WorldBound` and receives it through `Init(World)`.

### The frame loop

`World.Update(dt)` runs:

```
NextFrame
→ BeforeAll.Execute → BeforeUpdate.Execute      (command buffers — deferred structural changes applied here)
→ Events.Promote + Events.Trim                  (staged → live, retire consumed/aged events)
→ InitializeSystems.Run                         (Awake + Start pending objects, then MarkReady)
┌─ iteration guard ─────────────────────────────
│ → Systems.UpdateEvents                        (EventExecute methods)
│ → Systems.Update                              (Execute methods)
│ → Objects.Update                              (IObjectUpdate components)
└───────────────────────────────────────────────
→ AfterUpdate.Execute
```

`FixedUpdate` and `LateUpdate` follow the same shape but **skip `InitializeSystems`**. A few asymmetries are worth remembering:

- `BeforeAll` runs **only** at the start of `Update`.
- `AfterAll` runs **only** at the end of `LateUpdate`.
- `FixedUpdate` runs neither `BeforeAll` nor `AfterAll`.
- `Universe.Tick` routes its fixed step into **`Update`**, so the `FixedUpdate` phase only runs if you call it yourself.

Only `Systems.*` and `Objects.*` run inside the **iteration guard**, where direct structural changes are blocked (see [Deferred structural changes](#deferred-structural-changes)). `World.Frame` advances once per phase call; `World.Version` is a separate watermark that advances on component `MarkReady`/`Bump` and drives reactive cursors.

### Entities

`EosEntity` is an immutable `readonly struct`: `(int Id, ushort Version, World)`. The `Version` increments on destroy, so stale handles are detectable. `Name` is resolved from the world, not stored on the struct. `EosEntity.Null` has `Id = -1`.

```csharp
var e = new EosEntity(world, "Enemy");   // created inactive
e.Add<Health>();
e.On();                                  // activate → Awake/Start run next frame

e.IsValid;                               // world != null && version matches
e.IsActive;
e.Off();                                 // deactivate (re-runs RefreshReady across its components)
e.Destroy();                             // invalidates the handle (version bump)
```

Give an entity a **serialization-stable** string handle, independent of its runtime id:

```csharp
world.Entities.SetStableKey(e, "boss-01");
world.Entities.TryFind("boss-01", out var boss);
```

### Components

`EosObject` is the base class for every component. Its lifecycle:

1. **Add** — `entity.Add<T>()` allocates a slot in `Storage<T>` and registers the object in the *waiting* pool.
2. **Awake → Start** — `InitializeSystems` runs these (in that order) for waiting objects on a valid, **active** entity, then `MarkReady` signals the `[New]` channel and moves the object to the *inited* pool.
3. **Update** — per-frame, if the component implements `IObjectUpdate` / `IObjectFixedUpdate` / `IObjectLateUpdate`.
4. **Dispose** — on `Remove`/`Clear`: runs traced disposables, then `OnDispose`, then unregisters.

```csharp
public class Velocity : EosObject, IObjectUpdate
{
    public float X, Y;

    protected override void OnAwake()  { /* resolve dependencies */ }
    protected override void OnStart()  { /* one-time setup */ }
    protected override void OnDispose(){ /* cleanup */ }

    public void Update(float dt)
    {
        // Per-object update (runs after systems each frame).
        if (TryGet<Health>(out var h) && h.Current <= 0) Disable();
    }
}
```

A component is only visited by queries and per-object updates when it is **enabled**:

```
IsEnabled = IsAwaken && IsStarted && enabled && Entity.IsActive
```

Toggle with `Enable()` / `Disable()` / `SetEnabled(bool)`. Call `Bump()` from inside a component to signal the `[Bumped]` reactive channel (deduped to once per frame). `Trace(disposable)` registers a disposable to be cleaned up automatically on dispose.

Convenience accessors are available both on the entity (`EntityExtensions`) and inside a component:

```csharp
// On an entity handle:
e.Add<T>(); e.Get<T>(); e.TryGet<T>(out var t); e.Has<T>(); e.Remove<T>();

// Inside an EosObject (operate on the owning entity):
Add<T>(); Get<T>(); TryGet<T>(out var t); Has<T>(); Remove<T>(); Services.Get<IFoo>();
```

### Systems & queries

An `EosSystem` declares one or more `Execute(...)` methods. **The parameter types define the query** — the system fires once per matching, enabled component set.

| Parameter            | Meaning                                                               |
|----------------------|----------------------------------------------------------------------|
| `T : EosObject`      | mandatory concrete component                                         |
| `[Optional] T`       | optional concrete component (may be `null`)                          |
| `IFoo`               | mandatory interface component (fan-out across all implementations)  |
| `[Each] IFoo`        | cartesian fan-out over all matching implementations                 |
| `[New] T`            | reactive: fires only when `T` was recently added                    |
| `[Bumped] T`         | reactive: fires only when `Bump()` was called on `T` this window    |
| `EosEntity`          | receives the owning entity                                          |
| `float`              | receives delta time                                                 |

```csharp
public class MovementSystem : EosSystem
{
    // Matches entities that have BOTH Position and Velocity.
    void Execute(EosEntity e, Position pos, Velocity vel, float dt)
    {
        pos.X += vel.X * dt;
        pos.Y += vel.Y * dt;
    }
}
```

Systems are **discovered automatically** at `World.Init()` — every non-abstract `EosSystem` is found and wired. The `UpdateType` property routes a whole system to the `Update`, `FixedUpdate`, or `LateUpdate` phase; `UpdateWhen()` (and `IsEnabled`) gate it at runtime:

```csharp
public class PhysicsSystem : EosSystem
{
    public override UpdateType UpdateType => UpdateType.FixedUpdate;
    protected override bool UpdateWhen() => Services.Get<IGameState>().IsPlaying;

    void Execute(Rigidbody rb, float dt) { /* ... */ }
}
```

### Query filters

Method-level attributes refine a query without adding parameters:

```csharp
public class AiSystem : EosSystem
{
    [Include(typeof(Brain))]      // must also have Brain (not bound as a parameter)
    [Exclude(typeof(Stunned))]    // must NOT have Stunned
    [WithTag("Enemy")]            // tag filters: WithTag / WithoutTag / WithAnyTag / WithOneTag
    void Execute(Position pos, Velocity vel) { /* ... */ }
}
```

### Reactive queries

Reactive parameters fire only on a change edge, tracked by a world-version cursor — no per-frame scanning.

```csharp
public class SpawnFxSystem : EosSystem
{
    // Fires once, the frame after a Health component becomes ready.
    void Execute([New] Health health, EosEntity e) { /* play spawn effect */ }
}

public class DirtyTransformSystem : EosSystem
{
    // Fires when something called transform.Bump() this version window.
    void Execute([Bumped] Transform t) { /* re-upload to GPU */ }
}
```

> **Caveat:** a reactive system's cursor advances **every frame, even while its group is disabled** — so edges that occur during a disabled period are dropped when the group re-enables. New reactive systems start their cursor at the current version, so they never fire for components that already existed.

### Events

One-frame, read-once events modelled on the data-oriented pattern: emit now, every interested system reads exactly once, then it is gone — no entities, no `EosObject` overhead. Events are plain **structs** flowing through per-type channels.

```csharp
public struct DamageEvent
{
    public EosEntity Target;
    public int Amount;
}

public class CombatSystem : EosSystem
{
    void Execute(Weapon w, EosEntity attacker)
    {
        // Safe to emit mid-iteration: no closures, no structural change.
        World.Event(new DamageEvent { Target = w.CurrentTarget, Amount = w.Damage });
    }
}

public class HealthSystem : EosSystem
{
    // EventExecute runs BEFORE Execute in the same phase, fires once per event.
    void EventExecute(DamageEvent e)
    {
        if (e.Target.TryGet<Health>(out var h)) h.Current -= e.Amount;
    }

    void Execute(Health h) { /* normal query, runs after events */ }
}
```

Each phase: `Promote` moves staged events to live (events surface one tick after emit), and read-once is guaranteed by a **per-`EventExecute` cursor**, so multi-step `FixedUpdate` and phase ordering never double-read. An event lives until every registered consumer has advanced past it (min-cursor retirement), capped by `EventsContainer.MaxAge` (default 16 frames) so an undriven phase can't leak.

### Tags

`World.Tags` is a per-entity bitmask. Tag keys are `string` or any `enum` value, interned to bit indices; `[Flags]` enums expand to one bit per set flag.

```csharp
e.AddTag("Boss");
e.SetFlag(Faction.Hostile, true);
e.HasTag("Boss");
e.HasAllTags("Boss", "Elite");
e.HasAnyTag("Boss", "Miniboss");
e.RemoveTag("Boss");
e.ClearTags();
```

Tags serialize by descriptor (string, or enum type + value), so they survive round-trips.

### Deferred structural changes

During the iteration guard, direct structural changes (create / add / remove / destroy) throw by default — they must be **deferred** through an `EntityCommandBuffer`. The world exposes one buffer per loop point: `BeforeAll`, `BeforeUpdate`, `AfterUpdate`, `BeforeFixedUpdate`, `AfterFixedUpdate`, `BeforeLateUpdate`, `AfterLateUpdate`, `AfterAll`.

A fluent API chains conditionals and operations:

```csharp
public class DeathSystem : EosSystem
{
    void Execute(EosEntity e, Health h)
    {
        if (h.Current > 0) return;

        // Deferred: applied when AfterUpdate runs, outside the iteration guard.
        World.AfterUpdate
            .Schedule(e)
            .When<Loot>()                 // only if it has Loot
            .Add<Corpse>(c => c.DecayTime = 5f)
            .RemoveTag("Alive")
            .Destroy();
    }
}
```

`Create(...)` returns a `DeferredEntity` you can schedule further ops against; it resolves when the buffer runs:

```csharp
World.BeforeUpdate
    .Create("Projectile")                 // active immediately when created via a buffer
    .Add<Position>(p => { p.X = 0; p.Y = 0; })
    .Add<Velocity>(v => { v.X = 10; })
    .AddTag("Bullet");
```

Available ops: `When<T>` / `If(predicate)` / `WhenTag` / `WhenNoTag` / `WhenAnyTag` / `WhenOneTag` (conditions), `Add<T>` / `Add<T>(configure)` / `Change<T>` / `ChangeOrAdd<T>` / `Remove<T>` / `Destroy`, and `AddTag` / `RemoveTag` / `SetFlag` / `ClearTags`.

The behaviour is governed by `World.StructuralChangePolicy` (`Throw` by default; set to `Warn` or `Allow` to mutate in place).

### System groups & ordering

Assign a system to a group with `[Group(typeof(MyGroup))]`. Groups nest by inheritance and support **hierarchical** enable/disable — a group counts as enabled only if it and all ancestors are enabled.

```csharp
public class GameplayGroup : SystemGroup { }
public class AiGroup : GameplayGroup { }   // nested under Gameplay

[Group(typeof(AiGroup))]
public class PatrolSystem : EosSystem { void Execute(Patrol p) { } }

// Toggle at runtime:
world.SystemGroups.Disable<AiGroup>();
world.SystemGroups.Enable<GameplayGroup>();
```

Ordering within the same group level is a deterministic topological sort: `[UpdateAfter(typeof(Other))]` / `[UpdateBefore(typeof(Other))]` are edges; `[UpdateOrder(int)]` (or `UpdateOrderPhase.BeforeAll` / `AfterAll`) is the tie-break priority, then type name, then discovery index. Cycles throw.

```csharp
[UpdateAfter(typeof(MovementSystem))]
[UpdateOrder(100)]
public class CameraFollowSystem : EosSystem { void Execute(Camera c) { } }
```

### Context & services

`World.Context` is a typed blackboard of **struct** values — a lightweight way to share state without a component:

```csharp
world.Context.Set(new GameTime { Elapsed = 0 });
world.Context.TryGet<GameTime>(out var t);

// Inside a system, Context adds a per-system change watermark:
public class ClockSystem : EosSystem
{
    void Execute(float dt)
    {
        if (Context.Changed<GameTime>(out var time))
            World.Event(new TimeChangedEvent { Elapsed = time.Elapsed });
    }
}
```

`World.Services` / `World.ServiceRegistry` form a per-world service locator. Register services **before** driving the world (registration is rejected during iteration):

```csharp
world.ServiceRegistry.Register<IAudio>(new AudioBackend());
// later, anywhere with World access:
Services.Get<IAudio>().Play("hit");
```

### Generic components

`EosObject` subclasses may be generic. A **closed** generic (`Incarnation<Transform>`) is an ordinary concrete component — `Storage<T>` keys by `typeof(T)`, so each closed type gets its own dense array. Query it one of two ways:

```csharp
// 1. By closed type — behaves like any concrete parameter.
void Execute(Incarnation<Transform> inc) { }

// 2. By a non-generic interface it implements — fans out across every closed variant.
//    [Each] is REQUIRED here, otherwise an entity carrying two variants fires only once.
void Execute([Each] IIncarnation inc) { }
```

The idiom: keep type-dependent work behind a non-generic interface and keep systems non-generic. Serialization of closed generics needs no per-type registration.

> **Open-generic systems are unsupported.** A system that needs a concrete `T` must be a closed, named subclass: `class FooSystem : BarSystem<Baz>`.

### Incarnation — the view seam

`Incarnation<TView>` bridges a component to an external view object (e.g. a Unity `GameObject`) **without the core ever naming the view type**. On `Awake` it resolves an `IIncarnationBinder<TView>` from the `IncarnationBridge` registry and instantiates the view; on `Dispose` it destroys it. Built-in `IncarnationSync*System`s call `Sync` / `SyncFixed` / `SyncLate` each phase.

```csharp
// In the consumer/engine assembly:
public class TransformBinder : IIncarnationBinder<GameObject>
{
    public GameObject Instantiate(EosEntity e, string id) => Object.Instantiate(Resources.Load<GameObject>(id));
    public void Destroy(EosEntity e, GameObject view)     => Object.Destroy(view);
    public void Sync(EosEntity e, GameObject view)        => view.transform.position = e.Get<Position>().ToVector3();
    public void SyncFixed(EosEntity e, GameObject view)   { }
    public void SyncLate(EosEntity e, GameObject view)    { }
}

// Register once at startup:
IncarnationBridge.Register<GameObject>(new TransformBinder());

// Author-side: attach a view to an entity.
var inc = entity.Add<Incarnation<GameObject>>();
inc.Setup("Prefabs/Enemy");
```

This is the primary attachment point for a rendering/engine bridge.

### Serialization

`WorldSerializer.Capture()` walks every serializable world into a `UniverseSnapshot` of plain records (entities with names/active/stable-key/tags, context values, and per-type component bags). `Restore` rebuilds entities (remapping ids), re-adds components, and replays serialized data.

```csharp
UniverseSnapshot snapshot = WorldSerializer.Capture();
// ... persist via your own format (the snapshot is plain objects) ...
WorldSerializer.Restore(snapshot);
```

Components opt into data by implementing `IObjectSerializable` (`DataType` / `SerializeData` / `DeserializeData`); component **presence** alone is restored even without it. `World.IsSerializable` and per-entity serializable flags exclude transient state. Persistence itself is the consumer's job through the `WorldLoader.OnSave` / `WorldLoader.OnLoad` hooks. Type resolution is **version-tolerant** — assemblies are matched by simple name, so snapshots survive assembly version drift (closed generics included).

### Reflection vs codegen

Two interchangeable paths produce identical results:

- **Reflection (default).** `World.Init()` reflects over every `EosSystem`, builds a delegate per `Execute` / `EventExecute` from the parameter shape, and invokes it. Correct and zero-config, but it builds `object[]` argument arrays and boxes per match.
- **Codegen (opt-in, zero-alloc).** Run the source generator to emit a `.g.cs` registry that registers itself through a `[ModuleInitializer]`. When a provider is present, the runner uses generated, typed, allocation-free bodies.

```csharp
// Re-run after adding, removing, or changing systems (e.g. from a build step or editor tool):
string path = SystemRegistryGenerator.Generate(outputDirectory: "_Generated");
```

Any method whose shape can't be typed (or a stale registry) **falls back to reflection per-method with a warning** — nothing breaks, it just deoptimizes.

### Logging, profiling & debug draw

```csharp
// Logging: a static ring buffer (128 entries). Always pass nameof(TheClass) as the source.
EosLog.OnLog = entry => Console.WriteLine(entry);
EosLog.Warning("Low on ammo", nameof(WeaponSystem));
EosLog.Error("Null target", nameof(CombatSystem));
string dump = EosLog.Dump();

// Profiling: off by default (zero overhead). World phases and every system body are auto-instrumented.
EosProfiler.Backend = new AggregatedProfilerBackend();
EosProfiler.Enabled = true;
// ... run some frames ...
string report = ((AggregatedProfilerBackend)EosProfiler.Backend).Dump(reset: true);

// Debug draw: override the empty virtuals and draw with whatever the consumer assembly has.
public class HitboxSystem : EosSystem
{
    public override void OnDebugDraw() { /* e.g. UnityEngine.Gizmos.DrawWireSphere(...) */ }
}
// Trigger the pass from your engine loop:
Universe.DebugDraw();
```

`EosObject.OnDebugDraw()` and `EosSystem.OnDebugDraw()` are empty virtuals; the core owns only the dispatch hook, not a drawing API. Each draw is wrapped in `try/catch + EosLog.Error` so one bad draw can't kill the pass.

### Static lifecycle

Static state that outlives a `World` — `EosLog.OnLog`, `WorldLoader` hooks, `IncarnationBridge` binders — is reset by `EosDomainReset.Reset()`. Call it on domain reload (and before re-`Boot`) so handlers and registrations don't leak across sessions. `Universe.Boot()` itself disposes the previous default world and re-initializes, so re-booting is safe.

```csharp
EosDomainReset.Reset();   // clear static handlers/registrations (e.g. on editor domain reload)
Universe.Boot();
```

---

## Project layout

```
Attributes/      Query, ordering, group, and tag attributes ([New], [Each], [Group], [WithTag], ...)
CodeGen/         Source generator + shared shape/signature model for zero-alloc system dispatch
Core/            Universe, World, services, typed context, structural-change policy
Diagnostics/     World debug helpers
Entities/        EosEntity struct + EntitiesContainer (alive list, free stack, stable keys)
Events/          EventChannel<T> + EventsContainer (one-frame, read-once structs)
Extensions/      Entity & tag convenience extension methods
Loader/          IncarnationBridge, IIncarnationBinder, EosDomainReset
Logging/          EosLog ring buffer + log records
Objects/         EosObject base, Incarnation<TView>, per-object update interfaces
Profiling/        EosProfiler facade + swappable backends
Serialization/   WorldSerializer, WorldLoader hooks, snapshot records, serializable interfaces
Storage/         Storage<T> dense sparse-set + ObjectsStorageMap registry
Systems/         EosSystem, SystemsRunner, groups, command buffers, initialize runner, incarnation sync
Tags/            TagRegistry, TagsContainer bitmask, TagFilter
```

---

## Design notes & constraints

- **Single-threaded by design.** EOS makes no concurrency guarantees; drive it from one thread.
- **No comments in the source.** The codebase deliberately carries no inline comments — behaviour is expressed in code and documented here and in `CLAUDE.md`.
- **Don't replace storage instances on reset.** System closures and generated bodies hold direct references to `Storage<T>`, event channels, and context. `Reset()` clears their **contents** but keeps the instances — replacing them would break every existing query.
- **Open generics are unsupported** by both the reflection and codegen paths — use closed, named subclasses.
- **Engine-agnostic core.** There are no `UnityEngine` (or any engine) references in the core. Rendering, input, and persistence live in a separate consumer assembly that plugs in through the `Incarnation`, service, and `WorldLoader` seams.

For a deeper architectural walkthrough — storage internals, reactive watermarks, event retirement, the topological sort — see [`CLAUDE.md`](./CLAUDE.md).

---

## License

EOS is released under the [MIT License](./LICENSE) — use it for anything, commercial or otherwise. The only condition is that the copyright notice and license text travel with substantial copies of the source.
