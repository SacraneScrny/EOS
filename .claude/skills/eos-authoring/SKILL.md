---
name: eos-authoring
description: Procedural recipes for extending the EOS ECS framework in this repo — adding or changing systems, components, events, tags, or Incarnation view binders, and regenerating the zero-alloc system codegen. Use when writing or modifying any EosSystem / EosObject, emitting or consuming events, wiring the Unity/view bridge, or after changing the set of systems. Complements CLAUDE.md (the architecture reference) with step-by-step recipes and the easy-to-miss gotchas.
---

# Authoring EOS

Procedural recipes for extending this single-threaded ECS. `CLAUDE.md` is the architecture reference (the "why"); this is the "how to do it without tripping the known wires."

## Golden rules (every change)

- **No comments of any kind.** 4-space indent, no tab alignment.
- **All error handling** via `try/catch` + `EosLog.Error/Warning(msg, nameof(TheClass))` — never swallow silently. Pass `nameof(TheClass)` as the source.
- **Never mutate structure during iteration.** Inside a system's `Execute`/`EventExecute`, defer `Create`/`Add`/`Remove`/`Destroy`/tag changes through an `EntityCommandBuffer` (`World.AfterUpdate` etc.). Direct mutation throws (`StructuralChangePolicy.Throw`).
- **After adding/removing/renaming/reshaping any `Execute`/`EventExecute`, regenerate the codegen** (see "Regenerate codegen"). The reflection path keeps working without it, but the typed zero-alloc path goes stale and falls back with warnings.
- Validate by reading — there is no build/test runner in the core library. Errors surface only in the consumer build.

## Add a component (`EosObject`)

```csharp
public class Health : EosObject
{
    public int Value;

    protected override void OnAwake() { }
    protected override void OnStart() { }
    protected override void OnDispose() { }
}
```

- Attach with `entity.Add<Health>()`; query/remove with `entity.Get/Has/TryGet/Remove<Health>()`. Storage auto-creates on first use.
- **Awake/Start only run when the entity is active.** A bare `new EosEntity(world, name)` is **inactive** — call `entity.On()` (or create via ECB, which makes it active). Otherwise the component sits in the waiting pool forever.
- Per-frame work: implement `IObjectUpdate` (`void OnUpdate(float dt)`), `IObjectFixedUpdate`, or `IObjectLateUpdate`. `IsEnabled` comes from `EosObject` — only ready+enabled objects are visited.
- Reactive signal: call `Bump()` inside the component to fire the `[Bumped]` channel (deduped once per frame).
- Emit an event from a component: `Entity.World.Event(new SomeEvent { ... })`.

### Make a component serializable (opt-in)

```csharp
public class Health : EosObject, IObjectSerializable
{
    public int Value;

    Type IObjectSerializable.DataType => typeof(int);
    object IObjectSerializable.SerializeData() => Value;
    void IObjectSerializable.DeserializeData(object data, IDeserializeContext ctx)
        => Value = (int)data;
}
```

Use `ctx.Resolve(localId)` to turn a serialized local entity id back into a live `EosEntity`. Component *presence* is restored even without `IObjectSerializable`; only the *data* needs it.

## Add a system (`EosSystem`)

```csharp
public class MoveSystem : EosSystem
{
    void Execute(Position p, Velocity v, float dt)
        => p.X += v.X * dt;
}
```

Systems are auto-discovered at `World.Init()` — no registration. The `Execute` parameter shape *is* the query:

| Param | Meaning |
|---|---|
| `T : EosObject` | mandatory concrete component |
| `[Optional] T` | optional concrete component |
| `IFoo` | mandatory interface component (fan-out over implementations) |
| `[Each] IFoo` | cartesian fan-out over all matching implementations |
| `[New] T` | reactive: fires when T was just `MarkReady`'d |
| `[Bumped] T` | reactive: fires when `Bump()` ran this version window |
| `EosEntity` | the owning entity |
| `float` | delta time |

Class-level knobs:
- `[Include(typeof(T))]` / `[Exclude(typeof(T))]` — has / not-has filters.
- `[WithTag(...)]` / `[WithoutTag(...)]` / `[WithAnyTag(...)]` / `[WithOneTag(...)]` — tag filters.
- `[Group(typeof(MyGroup))]` — assign to a `SystemGroup` (nest by inheritance; `World.SystemGroups.Enable<T>()/Disable<T>()`).
- `[UpdateAfter(typeof(X))]` / `[UpdateBefore(typeof(X))]` / `[UpdateOrder(int)]` (or `UpdateOrderPhase.BeforeAll/AfterAll`) — deterministic ordering; cycles throw.
- `public override UpdateType UpdateType => UpdateType.FixedUpdate;` — route the whole system to Update/FixedUpdate/LateUpdate.
- `protected override bool UpdateWhen() => ...;` — runtime gate.

Gotchas:
- **Reactive systems start with their cursor at the current version** — they do not fire for components that already existed when the system was created.
- A reactive system's cursor advances every frame **even while its group is disabled**, so events during the disabled window are dropped on re-enable.
- **Open-generic systems are unsupported.** If a system needs a concrete `T` in its body, make it a closed, named subclass: `class FooSystem : BarSystem<Baz>`.

## Add an event

Events are one-frame, read-once **structs**.

```csharp
public struct DamageEvent { public EosEntity Target; public int Amount; }

// emit (safe mid-iteration): from a system World.Event(...), from a component Entity.World.Event(...)
World.Event(new DamageEvent { Target = e, Amount = 5 });

// consume — runs before Execute in the same phase, ordered by the same group/order graph
class DamageSystem : EosSystem
{
    void EventExecute(DamageEvent e) { }          // optionally (DamageEvent e, float dt)
    void Execute(Health h) { }
}
```

Each `EventExecute` reads every event of its type exactly once (per-consumer cursor). Events surface one tick after emit (`Promote`), and retire once every consumer has read them (min-cursor), with a `MaxAge` hard cap (default 16 frames).

## Add an Incarnation binder (the view / Unity seam)

The core never references the view type. The consumer assembly implements a binder and registers it:

```csharp
class GameObjectBinder : IIncarnationBinder<GameObject>
{
    public GameObject Instantiate(EosEntity entity, string incarnationId) { ... }
    public void Destroy(EosEntity entity, GameObject view) { ... }
    public void Sync(EosEntity entity, GameObject view) { ... }       // Update phase
    public void SyncFixed(EosEntity entity, GameObject view) { ... }  // FixedUpdate phase
    public void SyncLate(EosEntity entity, GameObject view) { ... }   // LateUpdate phase
}

IncarnationBridge.Register<GameObject>(new GameObjectBinder());
```

Attach a view to an entity: `entity.Add<Incarnation<GameObject>>().Setup("prefabId");` (then activate the entity). `Incarnation<TView>` resolves the binder on Awake, instantiates the view, dispatches Sync/SyncFixed/SyncLate via the `IncarnationSync*System`s, and destroys the view on Dispose. The `Id` round-trips through serialization automatically.

Register binders/handlers from a `[RuntimeInitializeOnLoadMethod]`-style entry point, and call `EosDomainReset.Reset()` on domain reload / before re-`Boot` so static registrations don't leak.

## Regenerate codegen (zero-alloc path)

After any change to the set of `Execute`/`EventExecute` methods:

```csharp
string path = SystemRegistryGenerator.Generate(outputDirectory: "_Generated");
```

- Run this from a host where the game assemblies are loaded (e.g. a Unity editor menu item / pre-build step) — it reflects over the live AppDomain.
- It writes `EosGeneratedSystems.g.cs`, which self-registers via `[ModuleInitializer]` into `GeneratedSystems.Provider`. Include that file in the consumer build.
- When a provider is present, `SystemsRunner` uses typed allocation-free bodies and calls `PreserveStorages` first. Unsupported shapes / a stale registry fall back to reflection per-method with a warning — correct but deoptimized.
- The two paths must produce identical results; if you touch the generator, verify reflection-vs-codegen parity.

## Deferred structural changes from inside a system

```csharp
World.AfterUpdate.Schedule(entity)
    .When<Health>()
    .Change<Health>(h => h.Value -= 1)
    .Remove<Shield>();

var deferred = World.AfterUpdate.Create("Spawned")   // created active when the buffer runs
    .Add<Position>(p => p.X = 10)
    .AddTag(Faction.Enemy);
```

Buffers: `BeforeAll`, `BeforeUpdate`, `AfterUpdate`, `BeforeFixedUpdate`, `AfterFixedUpdate`, `BeforeLateUpdate`, `AfterLateUpdate`, `AfterAll` — each runs at its point in the frame loop.

## Pitfall checklist

- [ ] Regenerated codegen after reshaping systems?
- [ ] Entity activated (`.On()` / created via ECB) so its components actually Awake?
- [ ] All structural changes inside systems deferred through an ECB?
- [ ] No comments, 4-space indent, `try/catch` + `EosLog` with `nameof` source?
- [ ] Reactive system not expected to fire for pre-existing components?
- [ ] System needing a concrete generic `T` is a closed, named subclass?
- [ ] `Reset()` of any container does **not** clear the storage `_map`/`_byInterface` or the events `_channels` map (closures hold direct references).
