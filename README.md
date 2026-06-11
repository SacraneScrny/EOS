# EOS

**EOS** is a single-threaded **E**ntity-**C**omponent-**S**ystem framework for C#, designed around cache-friendly access patterns: components live in dense, linearly-iterated arrays. It is a pure class library with **zero external dependencies** and **zero engine references** — the core knows nothing about Unity, Godot, or any renderer. Engine integration plugs in through a small set of well-defined seams (see [EOS.Unity](#eosunity--the-unity-bridge) for the official Unity bridge).

EOS borrows the load-bearing ideas of data-oriented designs — dense sparse-set storage, deferred structural changes, one-frame read-once events, reactive change channels — while keeping an approachable, object-oriented authoring surface: components are classes, systems are classes, and a query is just a method signature.

```csharp
public class MovementSystem : EosSystem
{
    void Execute(Position pos, Velocity vel, float dt)
    {
        pos.X += vel.X * dt;
        pos.Y += vel.Y * dt;
    }
}
```

That is a complete system. No registration, no archetype declarations, no query builders — the parameter list *is* the query.

---

## Highlights

- **Dense sparse-set storage.** Each component type lives in its own contiguous `Storage<T>`; iteration is linear and cache-friendly. Removal is swap-remove, so the dense array never fragments.
- **Signature-based queries.** A system's query is defined by the parameter types of its `Execute(...)` method — concrete components, interfaces, optionals, the owning entity, delta time.
- **Reactive channels.** `[New]` fires once when a component becomes ready; `[Bumped]` fires when a component signals a change. Both are watermark-based — no per-frame scanning.
- **One-frame events.** Plain structs flow through typed channels with guaranteed read-once semantics per consumer, even across multi-step fixed updates.
- **Deferred structural changes.** A fluent `EntityCommandBuffer` defers create/add/remove/destroy to safe points in the frame; direct structural changes during iteration throw by default.
- **Tags.** A per-entity bitmask keyed by `string` or any `enum` (including `[Flags]`), with first-class query filters.
- **Multiple worlds.** A static `Universe` owns a default world plus any number of additional, independently-driven worlds.
- **Serialization.** Snapshot any world into plain records and restore it — version-tolerant across assembly drift, cross-entity references remapped automatically.
- **Two execution paths.** Zero-config reflection by default; an opt-in source-generated registry for allocation-free system dispatch.
- **Engine bridge.** The `Incarnation<TView>` seam binds a component to an external view object without the core ever naming the view type.
- **Batteries included.** Ring-buffer logging, swappable profiling, system groups with hierarchical enable/disable, a typed per-world blackboard, and a per-world service locator.

---

## Table of contents

- [Requirements & installation](#requirements--installation)
- [Quick start](#quick-start)
- [Core concepts](#core-concepts)
  - [Universe & worlds](#universe--worlds)
  - [The frame loop](#the-frame-loop)
  - [Entities](#entities)
  - [Components](#components)
  - [Per-object updates](#per-object-updates)
  - [Systems & queries](#systems--queries)
  - [Query filters](#query-filters)
  - [Reactive queries](#reactive-queries)
  - [Imperative queries](#imperative-queries-ui-monobehaviours-tools)
  - [Events](#events)
  - [Tags](#tags)
  - [Deferred structural changes](#deferred-structural-changes)
  - [System groups & ordering](#system-groups--ordering)
  - [Context & services](#context--services)
  - [World bootstrap](#world-bootstrap--per-world-seeding)
  - [Generic components](#generic-components)
  - [Incarnation — the view seam](#incarnation--the-view-seam)
  - [Serialization](#serialization)
  - [Reflection vs codegen](#reflection-vs-codegen)
  - [Logging](#logging)
  - [Profiling](#profiling)
  - [Debug draw](#debug-draw)
  - [Diagnostics](#diagnostics)
  - [Static lifecycle](#static-lifecycle)
- [Project layout](#project-layout)
- [Design notes & constraints](#design-notes--constraints)
- [EOS.Unity — the Unity bridge](#eosunity--the-unity-bridge)

---

## Requirements & installation

EOS is a plain C# class library — there is no `.csproj`, no solution file, and no test runner in this repository. Drop the source folders into your project (or reference them from your own `.csproj`) and you are done. The core targets a modern C# language version (`readonly struct`, pattern matching, `[ModuleInitializer]` for the codegen path) and has **zero NuGet dependencies**.

Everything lives under the `EOS.*` namespaces:

```csharp
using EOS.Core;        // Universe, World, services, context, UpdateType
using EOS.Entities;    // EosEntity, EntitiesContainer
using EOS.Objects;     // EosObject, Incarnation<TView>, per-object update interfaces
using EOS.Systems;     // EosSystem, SystemGroup
using EOS.Attributes;  // [New], [Bumped], [Each], [Optional], [Group], [Include], [WithTag], ...
using EOS.Extensions;  // entity helpers: Add/Get/Has/Remove, On/Off, tag helpers
using EOS.Queries;     // world.Query<...>() imperative queries
using EOS.Events;      // EventsContainer (usually accessed via World.Event)
using EOS.Serialization; // WorldSerializer, WorldLoader, IObjectSerializable
using EOS.Loader;      // IncarnationBridge, WorldBootstrap, EosDomainReset
using EOS.Logging;     // EosLog
using EOS.Profiling;   // EosProfiler, AggregatedProfilerBackend
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
//    This one matches every entity that has an enabled, ready Health component.
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

        Universe.Shutdown();
    }
}
```

> Entities are **created inactive by default**. `Awake`/`Start` of their components are deferred until the entity is activated with `entity.On()`. Entities created through a command buffer (`World.AfterUpdate.Create(...)`) are active immediately.

---

## Core concepts

### Universe & worlds

`Universe` is the static root. It owns one default `World` plus a list of additional worlds.

```csharp
Universe.Boot();                          // create & init the default world (safe to re-boot)
var extra = Universe.CreateWorld("ui");   // additional world (key optional, must be unique)
Universe.TryGetWorld("ui", out var w);    // look one up by key
Universe.DestroyWorld(extra);             // dispose an additional world (never the default)
Universe.Shutdown();                      // dispose everything, IsBooted = false

Universe.Update(dt);                      // fan out Update to every non-manual world
Universe.FixedUpdate(dt);
Universe.LateUpdate(dt);
Universe.DebugDraw();                     // gizmo pass (see Debug draw)

Universe.Off();                           // pause the whole tick (Update/FixedUpdate/LateUpdate become no-ops)
Universe.On();
Universe.Reset();                         // reset every world's contents (keeps storage instances)

bool booted  = Universe.IsBooted;
bool running = Universe.IsEnabled;
var def      = Universe.DefaultWorld;     // IReadOnlyWorld
var others   = Universe.OtherWorlds;      // IReadOnlyList<IReadOnlyWorld>
```

A fixed-step accumulator is provided for frame-rate-independent simulation:

```csharp
// Calls Update (not FixedUpdate) zero or more times per real frame,
// clamped at maxSteps to avoid the spiral of death. If the backlog still
// exceeds one step after maxSteps, the remainder is dropped.
Universe.Tick(realDelta, fixedStep: 1f / 60f, maxSteps: 8);
```

A world with `IsManualUpdate = true` is skipped by `Universe.*` and must be driven directly (`world.Update(dt)`).

Two details worth knowing:

- `Universe.Boot()` disposes any previous worlds first, so re-booting is safe. After init it invokes the `WorldLoader.OnLoad` hook — if that returns a snapshot, the universe is **restored automatically** (see [Serialization](#serialization)).
- Worlds cannot be created, destroyed, reset, or toggled **while the universe is iterating** — those calls log an error and bail.

`World` owns all subsystems and wires them together. Anything that needs a back-reference to its world extends `WorldBound` and receives it through `Init(World)` / `OnInited()`.

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

`FixedUpdate` and `LateUpdate` follow the same shape (each promotes/trims events for its own phase) but **skip `InitializeSystems`**. A few asymmetries are worth remembering:

- `BeforeAll` runs **only** at the start of `Update`.
- `AfterAll` runs **only** at the end of `LateUpdate`.
- `FixedUpdate` runs neither `BeforeAll` nor `AfterAll`.
- `Universe.Tick` routes its fixed step into **`Update`**, so the `FixedUpdate` phase only runs if you call it yourself.
- New components `Awake`/`Start` only inside `Update` — a world driven exclusively through `FixedUpdate` never initializes them.

Only `Systems.*` and `Objects.*` run inside the **iteration guard**, where direct structural changes are blocked (see [Deferred structural changes](#deferred-structural-changes)). The `Before*/After*` buffers and `InitializeSystems` run outside it, so structural work there is unguarded by design.

`World.Frame` advances once per phase call. `World.Version` is a separate watermark that advances only on component `MarkReady`/`Bump` — reactive cursors compare against it. `World.CurrentPhase` reports which phase is running, and `World.AfterCurrentPhase` resolves to the matching after-buffer (`AfterUpdate` / `AfterFixedUpdate` / `AfterLateUpdate`) — handy for code that may be called from any phase.

### Entities

`EosEntity` is an immutable `readonly struct`: `(int Id, ushort Version, World)`. The `Version` increments on destroy, so stale handles are detectable. `Name` is resolved from the world, not stored on the struct. `EosEntity.Null` has `Id = -1`.

```csharp
var e = new EosEntity(world, "Enemy");                  // created INACTIVE by default
var f = new EosEntity(world, "Fx", active: true);       // or active right away
var g = new EosEntity(world, "Tmp", active: true,
                      isSerializable: false);            // excluded from snapshots

e.Add<Health>();
e.On();                                  // activate → Awake/Start run at the next Update

bool ok = e.IsValid;                     // world != null && id/version still match
bool on = e.IsActive;
e.Off();                                 // deactivate (re-runs RefreshReady across its components)
e.Destroy();                             // invalidates the handle (version bump)
```

Give an entity a **serialization-stable** string handle, independent of its runtime id:

```csharp
world.Entities.SetStableKey(e, "boss-01");
world.Entities.TryFind("boss-01", out var boss);
string key = world.Entities.GetStableKey(e);
```

Enumerate live entities without allocation:

```csharp
foreach (var entity in world.Entities.All()) { /* ... */ }
world.Entities.ForEach(entity => { /* ... */ });
```

### Components

`EosObject` is the base class for every component. Its lifecycle:

1. **Add** — `entity.Add<T>()` allocates a slot in `Storage<T>` and registers the object in the *waiting* pool.
2. **Awake → Start** — `InitializeSystems` runs these (in that order) for waiting objects on a valid, **active** entity, then `MarkReady` signals the `[New]` channel and moves the object to the *inited* pool. An exception in `OnAwake`/`OnStart` marks the object `IsFailed` and it never becomes ready.
3. **Update** — per-frame, if the component implements a per-object update interface (below).
4. **Dispose** — on `Remove`/`Clear`/entity destroy: runs traced disposables, then `OnDispose`, then unregisters.

```csharp
public class Velocity : EosObject
{
    public float X, Y;

    protected override void OnAwake()   { /* resolve dependencies (runs once) */ }
    protected override void OnStart()   { /* one-time setup after Awake */ }
    protected override void OnDispose() { /* cleanup */ }
    protected override void OnDebugDraw() { /* gizmos (see Debug draw) */ }
}
```

A component is only visited by queries and per-object updates when it is **enabled**:

```
IsEnabled = IsAwaken && IsStarted && enabled && Entity.IsActive
```

Toggle with `Enable()` / `Disable()` / `SetEnabled(bool)`. Call `Bump()` from inside a component to signal the `[Bumped]` reactive channel (deduped to once per frame); from outside, `entity.Bump<T>()` does the same. `Trace(disposable)` (or `Trace(params IDisposable[])`) registers disposables to be cleaned up automatically on dispose.

Convenience accessors exist both on the entity handle and inside a component:

```csharp
// On an entity handle (EOS.Extensions):
e.Add<T>(); e.Get<T>(); e.TryGet<T>(out var t); e.Has<T>(); e.Remove<T>(); e.Bump<T>();

// Inside an EosObject (operate on the owning entity):
Add<T>(); Get<T>(); TryGet<T>(out var t); Has<T>(); Remove<T>();
Services.Get<IFoo>();   // the owning world's service locator
```

### Per-object updates

For behaviour that belongs to one component rather than a system, implement an update interface. Each declares `bool IsEnabled { get; }` — already provided by `EosObject` — plus one method:

```csharp
using EOS.Objects.Interfaces;

public class Spinner : EosObject, IObjectUpdate
{
    public float DegreesPerSecond = 90f;
    public float Angle;

    public void OnUpdate(float deltaTime) => Angle += DegreesPerSecond * deltaTime;
}
```

| Interface            | Method                          | Runs in     |
|----------------------|---------------------------------|-------------|
| `IObjectUpdate`      | `OnUpdate(float deltaTime)`     | `Update`      |
| `IObjectFixedUpdate` | `OnFixedUpdate(float deltaTime)`| `FixedUpdate` |
| `IObjectLateUpdate`  | `OnLateUpdate(float deltaTime)` | `LateUpdate`  |

Per-object updates run **after** systems in the same phase and only for ready, enabled components.

### Systems & queries

An `EosSystem` declares one or more `Execute(...)` methods. **The parameter types define the query** — the system fires once per matching, enabled component set.

| Parameter            | Meaning                                                              |
|----------------------|----------------------------------------------------------------------|
| `T : EosObject`      | mandatory concrete component                                         |
| `[Optional] T`       | optional concrete component (may be `null`)                          |
| `IFoo`               | mandatory interface component (any implementation)                   |
| `[Each] IFoo`        | cartesian fan-out over **all** matching implementations              |
| `[New] T`            | reactive: fires only when `T` recently became ready                  |
| `[Bumped] T`         | reactive: fires only when `Bump()` was called on `T` this window     |
| `EosEntity`          | receives the owning entity                                           |
| `float`              | receives delta time                                                  |

```csharp
public class MovementSystem : EosSystem
{
    // Matches entities that have BOTH Position and Velocity (and are ready/enabled).
    void Execute(EosEntity e, Position pos, Velocity vel, float dt)
    {
        pos.X += vel.X * dt;
        pos.Y += vel.Y * dt;
    }
}
```

Systems are **discovered automatically** at `World.Init()` — every non-abstract `EosSystem` subclass is instantiated and wired. There is no manual registration step.

The `UpdateType` property routes the whole system to a phase; `UpdateWhen()` gates it per frame; `Awake()`/`Start()` are one-time lifecycle hooks; `On()`/`Off()` toggle the system at runtime (with `OnEnable`/`OnDisable` callbacks):

```csharp
public class PhysicsSystem : EosSystem
{
    public override UpdateType UpdateType => UpdateType.FixedUpdate;

    public override void Awake() { /* world is assigned, runs once */ }

    protected override bool UpdateWhen() => Services.Get<IGameState>().IsPlaying;

    void Execute(Rigidbody rb, float dt) { /* ... */ }
}

// elsewhere: pause one system without touching its group
mySystem.Off();
mySystem.On();
```

### Query filters

Method-level attributes refine a query without binding extra parameters:

```csharp
public class AiSystem : EosSystem
{
    [Include(typeof(Brain))]          // must also have Brain (params Type[])
    [Exclude(typeof(Stunned))]        // must NOT have Stunned
    [WithTag("Enemy")]                // tag filters: WithTag / WithoutTag / WithAnyTag / WithOneTag
    void Execute(Position pos, Velocity vel) { /* ... */ }
}
```

All tag filter attributes accept `params object[]` — strings and enum values mix freely:

```csharp
[WithAnyTag("Boss", Faction.Hostile)]
void Execute(Health h) { }
```

### Reactive queries

Reactive parameters fire only on a change edge, tracked by a world-version cursor — no per-frame scanning.

```csharp
public class SpawnFxSystem : EosSystem
{
    // Fires once, at the Update after a Health component becomes ready.
    void Execute([New] Health health, EosEntity e) { /* play spawn effect */ }
}

public class DirtyTransformSystem : EosSystem
{
    // Fires when something called transform.Bump() (or entity.Bump<Transform>()).
    void Execute([Bumped] Transform t) { /* re-upload to GPU */ }
}
```

> **Caveat:** a reactive system's cursor advances **every frame, even while its group is disabled** — edges that occur during a disabled period are dropped when the group re-enables. New reactive systems start their cursor at the current version, so they never fire for components that already existed.

### Imperative queries (UI, MonoBehaviours, tools)

Systems are the place for simulation logic, but view code that lives **outside** the tick — UI panels, MonoBehaviours, editor tooling — often needs to read world state on demand. `world.Query<...>()` (namespace `EOS.Queries`, an extension on `IReadOnlyWorld`) is the imperative counterpart to a system's `Execute` query: an allocation-free struct enumerator over the same **ready, enabled** components.

```csharp
using EOS.Queries;

// One component — yields the component (entity via component.Entity):
foreach (var health in world.Query<Health>())
    bar.SetFill(health.Current / (float)health.Max);

// Two or three — yields a QueryResult with .Entity + .Item1.. (deconstructable):
foreach (var (pos, vel) in world.Query<Position, Velocity>())
    DrawArrow(pos, vel);

// Fluent filters mirror the system attributes:
var enemies = world.Query<Health>()
    .With<Brain>()         // must also have a ready Brain
    .Without<Stunned>()    // must NOT have a ready Stunned
    .WithTag("Enemy");     // WithTag / WithoutTag / WithAnyTag / WithOneTag

int count = enemies.Count();
if (enemies.TryFirst(out var first)) { /* ... */ }
bool any = enemies.Any();
enemies.ForEach(h => h.Disable());
var list = enemies.ToList();
```

Multi-component queries pivot on the smallest storage and dedup by entity. Each fluent call returns a new immutable query struct (copy-on-write filter arrays — small allocations at configuration time only; enumeration stays alloc-free). Enumeration is read-only — structural changes during it hit `StructuralChangePolicy` exactly like inside a system. Reactive `[New]`/`[Bumped]` channels stay system-only by design.

### Events

One-frame, read-once events modelled on the data-oriented pattern: emit now, every interested system reads exactly once, then it is gone — no entities, no `EosObject` overhead. Events are plain **structs** flowing through per-type channels owned by `World.Events`.

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

    // Optional dt overload: void EventExecute(DamageEvent e, float dt)

    void Execute(Health h) { /* normal query, runs after events */ }
}
```

Mechanics:

- A system may declare several `EventExecute` methods for different event types. They are ordered through the same `[Group]` / `[UpdateBefore]` / `[UpdateAfter]` / `[UpdateOrder]` graph as `Execute` methods (sorted independently over the event list).
- Each phase runs `Promote` (staged → live, ascending sequence numbers — events surface one tick after emit) and `Trim` (drop retired events).
- **Read-once is guaranteed by a per-`EventExecute` cursor** (a sequence watermark), so multi-step `FixedUpdate` and phase ordering never double-read.
- Retirement is **min-cursor**: an event lives until every registered consumer of its type has advanced past it. `EventsContainer.MaxAge` (16 frames) is a hard cap so an undriven phase can't leak events.
- A consumer in a disabled group still advances its cursor (events are dropped for it), consistent with the reactive caveat above.

### Tags

`World.Tags` is a per-entity bitmask. Tag keys are `string` or any `enum` value, interned to bit indices; `[Flags]` enums expand to one bit per set flag.

```csharp
e.AddTag("Boss");
e.AddTag("Elite", "Miniboss");          // params overload
e.SetFlag(Faction.Hostile, true);       // enum keys work everywhere
e.HasTag("Boss");
e.HasAllTags("Boss", "Elite");
e.HasAnyTag("Boss", "Miniboss");
e.HasOneTag("Red", "Blue");             // exactly one
e.HasAnyIn<Faction>();                  // any tag from this enum type
e.RemoveTag("Boss");
e.ClearTags();
```

Tags serialize by descriptor (string, or enum type + value), so they survive snapshot round-trips even if bit indices change between sessions.

### Deferred structural changes

During the iteration guard, direct structural changes (create / add / remove / destroy / tag mutations) **throw by default** — they must be deferred through an `EntityCommandBuffer`. The world exposes one buffer per loop point:

```
BeforeAll, BeforeUpdate, AfterUpdate,
BeforeFixedUpdate, AfterFixedUpdate,
BeforeLateUpdate, AfterLateUpdate, AfterAll
```

…plus `World.AfterCurrentPhase`, which resolves to the after-buffer of whatever phase is currently running.

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
            .When<Loot>()                 // condition: only proceed if it has Loot
            .Add<Corpse>(c => c.DecayTime = 5f)
            .RemoveTag("Alive")
            .Destroy();
    }
}
```

`Create(...)` returns a `DeferredEntity` you can schedule further ops against; it resolves when the buffer runs. Entities created through a buffer are **active immediately**:

```csharp
var projectile = World.BeforeUpdate.Create("Projectile");
World.BeforeUpdate
    .Schedule(projectile)
    .Add<Position>(p => { p.X = 0; p.Y = 0; })
    .Add<Velocity>(v => { v.X = 10; })
    .AddTag("Bullet");
```

Semantics worth knowing:

- **Conditions short-circuit.** When a `When<T>` / `If` / `WhenTag…` op evaluates false, the *rest of that chain* is skipped for that entity.
- `Add<T>()` adds only if the component is missing; `Add<T>(configure)` configures only a newly-added component. Use `Change<T>(action)` (acts if present) or `ChangeOrAdd<T>(action)` (always acts) to mutate existing data.
- Ops scheduled *during* buffer execution are drained in the same pass (with a 10 000-op runaway guard).
- A reusable `CommandChain` can be built once and applied to many entities via `Schedule(entity, chain)`.

Available ops: `When<T>` / `If(predicate)` / `WhenTag` / `WhenNoTag` / `WhenAnyTag` / `WhenOneTag` (conditions), `Add<T>` / `Add<T>(configure)` / `Change<T>` / `ChangeOrAdd<T>` / `Remove<T>` / `Destroy`, and `AddTag` / `RemoveTag` / `SetFlag` / `ClearTags`.

The enforcement is governed by `World.StructuralChangePolicy` — `Throw` (default), `Warn`, or `Allow` to mutate in place.

### System groups & ordering

Assign a system to a group with `[Group(typeof(MyGroup))]`. Groups nest by **class inheritance** and support hierarchical enable/disable — a group counts as enabled only if it and all ancestors are enabled.

```csharp
public class GameplayGroup : SystemGroup { }
public class AiGroup : GameplayGroup { }   // nested under Gameplay

[Group(typeof(AiGroup))]
public class PatrolSystem : EosSystem { void Execute(Patrol p) { } }

// Toggle at runtime:
world.SystemGroups.Disable<AiGroup>();
world.SystemGroups.Enable<GameplayGroup>();
bool on = world.SystemGroups.IsEnabled(typeof(AiGroup));
```

Ordering within the same group level is a deterministic topological sort: `[UpdateAfter(typeof(Other))]` / `[UpdateBefore(typeof(Other))]` are hard edges; `[UpdateOrder(int)]` (or `UpdateOrderPhase.BeforeAll` / `AfterAll`, which map to `int.MinValue` / `int.MaxValue`) is the tie-break priority, then type name, then discovery index. Cycles throw at init.

```csharp
[UpdateAfter(typeof(MovementSystem))]
[UpdateOrder(100)]
public class CameraFollowSystem : EosSystem { void Execute(Camera c) { } }
```

### Context & services

`World.Context` is a typed blackboard of **struct** values — a lightweight way to share world-global state without an entity:

```csharp
world.Context.Set(new GameTime { Elapsed = 0 });
world.Context.TryGet<GameTime>(out var t);
world.Context.Has<GameTime>();
world.Context.Clear<GameTime>();

// Inside a system, Context adds a per-system change watermark:
public class ClockSystem : EosSystem
{
    void Execute(float dt)
    {
        if (Context.Changed<GameTime>(out var time))   // true once per change, per system
            World.Event(new TimeChangedEvent { Elapsed = time.Elapsed });
    }
}
```

Only context values implementing the marker interface `ISerializableContext` are captured into snapshots.

`World.Services` (`IServiceLocator`: `Get` / `TryGet` / `Has`) and `World.ServiceRegistry` (`IServiceRegistry`: adds `Register` / `Unregister`) form a per-world service locator:

```csharp
world.ServiceRegistry.Register<IAudio>(new AudioBackend());

// later, anywhere with World access (systems and components expose `Services`):
Services.Get<IAudio>().Play("hit");
```

Registration and unregistration are **rejected during iteration** (logged error) — wire services before driving the world. Registering the same type twice silently overwrites.

### World bootstrap — per-world seeding

`WorldBootstrap.Provider` (a `static Action<World>` in `EOS.Loader`) is the seam for seeding *every* world with services and context defaults. `World.Init()` **and** `World.Reset()` both funnel through it, so defaults survive a reset wipe:

```csharp
WorldBootstrap.Provider = world =>
{
    world.ServiceRegistry.Register<IRandom>(new XorShiftRandom());
    world.Context.Set(new Difficulty { Level = 1 });
};

Universe.Boot();   // default world (and every later world) gets seeded
```

The provider is null by default (no-op). It runs for the default world, worlds created via `Universe.CreateWorld`, and any created later. The core never populates it — the consumer does (the Unity bridge generates one from `[EosWorldBootstrap]` methods).

### Generic components

`EosObject` subclasses may be generic. A **closed** generic (`Incarnation<Transform>`) is an ordinary concrete component — `Storage<T>` keys by `typeof(T)`, so each closed type gets its own dense array. Query it one of two ways:

```csharp
// 1. By closed type — behaves like any concrete parameter.
void Execute(Incarnation<Transform> inc) { }

// 2. By a non-generic interface it implements — fans out across every closed variant.
//    [Each] is REQUIRED here, otherwise an entity carrying two variants fires only once.
void Execute([Each] IIncarnation inc) { }
```

The idiom: keep type-dependent work behind a non-generic interface and keep systems non-generic. Serialization of closed generics needs no per-type registration — type names resolve version-tolerantly, generic arguments included.

> **Open-generic systems are unsupported** by both execution paths. A system that needs a concrete `T` must be a closed, named subclass: `class FooSystem : BarSystem<Baz>`.

### Incarnation — the view seam

`Incarnation<TView>` bridges a component to an external view object (e.g. a Unity `GameObject`) **without the core ever naming the view type**. On `Awake` it resolves an `IIncarnationBinder<TView>` from the `IncarnationBridge` registry and instantiates the view; on `Dispose` it destroys it. Built-in `IncarnationSync*System`s call `Sync` / `SyncFixed` / `SyncLate` each phase through the typed binder — no casting or boxing in user code.

```csharp
// The binder contract (EOS.Loader):
public interface IIncarnationBinder<TView> where TView : class
{
    TView Instantiate(EosEntity entity, string incarnationId);
    void Destroy(EosEntity entity, TView view);
    void Sync(EosEntity entity, TView view);
    void SyncFixed(EosEntity entity, TView view);
    void SyncLate(EosEntity entity, TView view);
}

// In the consumer/engine assembly:
public class GameObjectBinder : IIncarnationBinder<GameObject>
{
    public GameObject Instantiate(EosEntity e, string id) => Object.Instantiate(Resources.Load<GameObject>(id));
    public void Destroy(EosEntity e, GameObject view)     => Object.Destroy(view);
    public void Sync(EosEntity e, GameObject view)        => view.transform.position = e.Get<Position>().ToVector3();
    public void SyncFixed(EosEntity e, GameObject view)   { }
    public void SyncLate(EosEntity e, GameObject view)    { }
}

// Register once at startup:
IncarnationBridge.Register<GameObject>(new GameObjectBinder());

// Author-side: attach a view to an entity.
var inc = entity.Add<Incarnation<GameObject>>();
inc.Setup("Prefabs/Enemy");    // the id is opaque to the core — the binder interprets it
entity.On();                   // Awake resolves the binder and instantiates the view
```

`Incarnation<TView>` exposes `Id` and `View`, and serializes its `Id` automatically, so views are re-instantiated on snapshot restore. A missing binder is not an error — the incarnation just stays viewless (logged at debug level). This is the primary attachment point for a rendering/engine bridge.

### Serialization

`WorldSerializer.Capture()` walks every serializable world into a `UniverseSnapshot` of plain records — entities (name / active / stable key / tags), context values, and per-type component bags. `Restore(snapshot)` rebuilds it all.

```csharp
UniverseSnapshot snapshot = WorldSerializer.Capture();
// ... persist however you like — the snapshot is plain objects, pick your own format ...
WorldSerializer.Restore(snapshot);
```

How restore works:

- **Two passes**: all entities are recreated first (runtime ids remapped, stable keys preserved), then components are re-added and their data deserialized. By the time any reference resolves, every entity already exists.
- Components opt into data by implementing `IObjectSerializable`:

```csharp
public class Follower : EosObject, IObjectSerializable
{
    public EosEntity Target;

    [Serializable] public class Data { public int TargetLocalId; }

    public Type DataType => typeof(Data);
    public object SerializeData() => new Data { TargetLocalId = Target.Id };
    public void DeserializeData(object data, IDeserializeContext ctx)
        => Target = ctx.Resolve(((Data)data).TargetLocalId);   // old local id → live entity
}
```

  Component **presence** alone is restored even without `IObjectSerializable`.
- `World.IsSerializable`, per-entity serializable flags (`new EosEntity(world, name, active, isSerializable: false)`), and the `ISerializableContext` marker exclude transient state.
- Type resolution is **version-tolerant** — assemblies are matched by simple name (generic arguments included), so snapshots survive assembly version drift.

Persistence itself is the consumer's job through two hooks:

```csharp
WorldLoader.OnSave = snapshot => File.WriteAllText("save.json", MyJson.Serialize(snapshot));
WorldLoader.OnLoad = () => File.Exists("save.json")
    ? MyJson.Deserialize<UniverseSnapshot>(File.ReadAllText("save.json"))
    : null;

WorldSerializer.Save();   // Capture() + OnSave in one call
Universe.Boot();          // Boot() invokes OnLoad and auto-restores a non-null snapshot
```

### Reflection vs codegen

Two interchangeable execution paths produce identical results:

- **Reflection (default).** `World.Init()` reflects over every non-abstract `EosSystem`, builds a delegate per `Execute` / `EventExecute` from the parameter shape, and invokes the body. Correct and zero-config, but it builds `object[]` argument arrays and boxes per match.
- **Codegen (opt-in, zero-alloc).** Run the source generator to emit a registry that registers itself through a `[ModuleInitializer]`. When a provider is present, the runner uses generated, typed, allocation-free bodies, and pre-creates every component `Storage<T>` before bodies bind to it.

```csharp
// Re-run after adding, removing, or changing systems (e.g. from a build step or editor tool):
string path = SystemRegistryGenerator.Generate(
    outputDirectory: "_Generated",                 // default
    @namespace:      "EOS.Generated",              // default
    className:       "EosGeneratedSystems",        // default
    fileName:        "EosGeneratedSystems.g.cs");  // default
```

Any method whose shape can't be typed — or a stale registry — **falls back to reflection per-method with a warning**. Nothing breaks; it just deoptimizes. `SystemShape` / `SystemSignature` are the shared shape-and-identity model both paths agree on.

### Logging

`EosLog` is a static ring buffer (1024 entries) with a single handler seam:

```csharp
EosLog.OnLog = entry => Console.WriteLine(entry);   // default writes to Console
EosLog.Debug("Spawned wave 3", nameof(WaveSystem)); // [Conditional("DEBUG")] — stripped in release
EosLog.Warning("Low on ammo", nameof(WeaponSystem));
EosLog.Error("Null target", nameof(CombatSystem));
string dump = EosLog.Dump();                        // the whole ring, oldest first
```

Levels are `Debug`, `Warning`, `Error`. Always pass `nameof(TheClass)` as the source argument — it becomes the `[EOS:Source]` prefix in consumers.

### Profiling

`EosProfiler` is a static facade over a swappable backend, **off by default** (zero overhead):

```csharp
EosProfiler.Backend = new AggregatedProfilerBackend();
EosProfiler.Enabled = true;
// ... run some frames ...
string report = ((AggregatedProfilerBackend)EosProfiler.Backend).Dump(reset: true);
// e.g. "World.Update: 1.234ms over 600 calls (0.002ms avg)" lines, sorted by total time
```

`World.Update / FixedUpdate / LateUpdate` and every system body are auto-instrumented via `EosProfiler.Sample(label)`, which returns a `readonly struct Scope` whose `Dispose` ends the span — balanced even if the body throws. Use the same pattern in your own code:

```csharp
using (EosProfiler.Sample("Pathfinding"))
{
    // measured region
}
```

A backend implements `IEosProfilerBackend` (`Begin(label)` / `End()`). The Unity bridge supplies a `ProfilerMarker`-based one.

### Debug draw

The core stays engine-free, so it owns only the dispatch hook, not a drawing API. `EosObject.OnDebugDraw()` and `EosSystem.OnDebugDraw()` are empty virtuals — override them and draw with whatever your consumer assembly has:

```csharp
public class HitboxSystem : EosSystem
{
    public override void OnDebugDraw() { /* e.g. UnityEngine.Gizmos.DrawWireSphere(...) */ }
}

// Trigger the pass from your engine loop (the Unity bridge does this from OnDrawGizmos):
Universe.DebugDraw();
```

The pass fans out `Universe → World.DebugDraw → Objects.DebugDraw` (inited objects) + `Systems.DebugDraw` (all systems), each wrapped in `try/catch + EosLog.Error` so one bad draw can't kill the pass. It runs inside the iteration guard, so accidental structural changes during a draw are caught by `StructuralChangePolicy`.

### Diagnostics

`WorldDebug` renders human-readable dumps of live state:

```csharp
string all    = WorldDebug.DumpUniverse();
string one    = WorldDebug.DumpWorld(world);
string entity = WorldDebug.DumpEntity(e);
```

### Static lifecycle

Static state that outlives a `World` — the `EosLog.OnLog` handler, `WorldLoader` hooks, `IncarnationBridge` binders — is reset by `EosDomainReset.Reset()` (which also calls `Universe.Shutdown()`). Call it on domain reload and before re-booting so handlers and registrations don't leak across sessions:

```csharp
EosDomainReset.Reset();   // clear static handlers/registrations (e.g. on editor domain reload)
Universe.Boot();
```

> `WorldBootstrap.Provider` is deliberately **not** cleared by `EosDomainReset` — consumers re-install it before every boot (the Unity bridge does this automatically at `SubsystemRegistration`).

---

## Project layout

```
Attributes/      Query, ordering, group, and tag attributes ([New], [Each], [Group], [WithTag], ...)
CodeGen/         SystemRegistryGenerator + shared shape/signature model for zero-alloc dispatch
Core/            Universe, World, services, typed context, structural-change policy, UpdateType
Diagnostics/     WorldDebug dump helpers
Entities/        EosEntity struct + EntitiesContainer (alive list, free stack, stable keys)
Events/          EventChannel<T> + EventsContainer (one-frame, read-once structs)
Extensions/      Entity & tag convenience extension methods
Loader/          IncarnationBridge, IIncarnationBinder, WorldBootstrap, EosDomainReset
Logging/         EosLog ring buffer + log records
Objects/         EosObject base, Incarnation<TView>, per-object update interfaces
Profiling/       EosProfiler facade + Null/Aggregated backends
Queries/         Imperative EntityQuery<...> for external (UI/tool) read access
Serialization/   WorldSerializer, WorldLoader hooks, snapshot records, serializable interfaces
Storage/         Storage<T> dense sparse-set + ObjectsStorageMap registry
Systems/         EosSystem, SystemsRunner, groups, command buffers, initialize runner, incarnation sync
Tags/            TagRegistry, TagsContainer bitmask, TagFilter
```

---

## Design notes & constraints

- **Single-threaded by design.** EOS makes no concurrency guarantees; drive it from one thread.
- **No comments in the source.** The codebase deliberately carries no inline comments — behaviour is expressed in code and documented here and in [`CLAUDE.md`](./CLAUDE.md).
- **Don't replace storage instances on reset.** System closures and generated bodies hold direct references to `Storage<T>` instances, event channels, and context cells. `Reset()` clears their **contents** but keeps the instances — replacing them would break every existing query.
- **Open generics are unsupported** by both the reflection and codegen system paths — use closed, named subclasses.
- **Engine-agnostic core.** There are no `UnityEngine` (or any engine) references in the core. Rendering, input, and persistence live in a separate consumer assembly that plugs in through the `Incarnation`, service, `WorldBootstrap`, and `WorldLoader` seams.
- **Errors are loud, never silent.** Every internal catch funnels through `EosLog.Error` / `EosLog.Warning`; invalid operations (boot-less updates, mid-iteration registrations) log instead of corrupting state.

For a deeper architectural walkthrough — storage internals, reactive watermarks, event retirement, the topological sort — see [`CLAUDE.md`](./CLAUDE.md).

---

## EOS.Unity — the Unity bridge

The official Unity integration lives in a separate repository: **EOS.Unity**. It drives the `Universe` from Unity's PlayerLoop, ships prefab-backed incarnation binders with optional view pooling, ScriptableObject entity presets and component sets, a runtime entity-assembly (socket/module) layer, attribute-driven bootstrap codegen, and a read-only World Inspector editor window — all through the seams above, without modifying the core.
