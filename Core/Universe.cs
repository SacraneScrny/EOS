using System.Collections.Generic;

using EOS.Logging;
using EOS.Serialization;

namespace EOS.Core
{
    /// <summary>The static root of the ECS. Owns the default world plus any additional worlds; <see cref="Boot"/> it once, then drive it each frame via <see cref="Update"/> / <see cref="FixedUpdate"/> / <see cref="LateUpdate"/> or <see cref="Tick"/>.</summary>
    public static class Universe
    {
        static int _nextId = 0;
        /// <summary>Whether ticking is currently enabled; <see cref="Off"/> makes the per-frame calls silent no-ops.</summary>
        public static bool IsEnabled { get; private set; }
        /// <summary>Whether <see cref="Boot"/> has run and the universe is ready to be driven.</summary>
        public static bool IsBooted { get; private set; }

        /// <summary>True while a tick is fanning out across worlds; world management is rejected during this window.</summary>
        public static bool IsIterating { get; private set; }
        static void BeginIteration() => IsIterating = true;
        static void EndIteration() => IsIterating = false;
        
        static float _accumulator;

        static World _defaultWorld;
        /// <summary>The always-present default world, exposed read-only.</summary>
        public static IReadOnlyWorld DefaultWorld => _defaultWorld;
        internal static World InternalDefaultWorld => _defaultWorld;

        static readonly List<World> _otherWorlds = new List<World>();
        /// <summary>The additional worlds created via <see cref="CreateWorld"/>, exposed read-only.</summary>
        public static IReadOnlyList<IReadOnlyWorld> OtherWorlds => _otherWorlds;
        internal static IReadOnlyList<World> InternalOtherWorlds => _otherWorlds;

        /// <summary>Count of all worlds including the default one.</summary>
        public static int TotalWorldsCount => 1 + _otherWorlds.Count;

        /// <summary>Disposes any previous worlds, creates and initializes a fresh default world, then auto-loads via <c>WorldLoader.OnLoad</c> and restores a returned snapshot. Safe to call again to re-boot.</summary>
        public static void Boot()
        {
            _defaultWorld?.Dispose();
            foreach (var w in _otherWorlds) w.Dispose();
            _otherWorlds.Clear();
            _nextId = 0;

            _defaultWorld = new();
            _defaultWorld.SetId(_nextId++);
            _defaultWorld.Init();
            _accumulator = 0f;
            
            IsEnabled = true;
            IsBooted = true;
            IsIterating = false;

            try
            {
                var snapshot = WorldLoader.OnLoad?.Invoke();
                if (snapshot != null)
                    WorldSerializer.Restore(snapshot);
            }
            catch (System.Exception ex)
            {
                EosLog.Error($"Failed to load universe snapshot: {ex}");
            }
        }
        /// <summary>Resets every world (re-seeding their bootstrap defaults) without re-creating them; rejected while iterating.</summary>
        public static void Reset()
        {
            if (!IsBooted) return;

            if (IsIterating)
            {
                EosLog.Error("Cannot reset the universe while it is iterating. Wait until the current update cycle is finished.");
                return;
            }
            
            _defaultWorld?.Reset();
            foreach (var world in _otherWorlds) world?.Reset();
            IsEnabled = true;
            IsIterating = false;
        }
        /// <summary>Disposes all worlds and clears <see cref="IsBooted"/>; call before re-booting or on domain reload.</summary>
        public static void Shutdown()
        {
            _defaultWorld?.Dispose();
            foreach (var w in _otherWorlds) w?.Dispose();
            _otherWorlds.Clear();
            _nextId = 0;
            _accumulator = 0;
            IsEnabled = false;
            IsBooted = false;
            IsIterating = false;
        }

        /// <summary>Creates and initializes an additional world with an optional unique <paramref name="key"/>; returns null if not booted, iterating, or the key is already taken.</summary>
        public static World CreateWorld(string key = null, bool isSerializable = true)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before creating worlds.");
                return null;
            }
            if (IsIterating)
            {
                EosLog.Error("Cannot create a new world while the universe is iterating. Wait until the current update cycle is finished.");
                return null;
            }
            if (!string.IsNullOrEmpty(key))
            {
                if (_defaultWorld?.Key == key)
                {
                    EosLog.Error($"World key '{key}' is already used by the default world.");
                    return null;
                }
                foreach (var existing in _otherWorlds)
                {
                    if (existing?.Key != key) continue;
                    EosLog.Error($"World key '{key}' is already used by world #{existing.Id}.");
                    return null;
                }
            }

            var world = new World();
            world.SetId(_nextId++);
            world.SetKey(key);
            world.IsSerializable = isSerializable;
            world.Init();
            _otherWorlds.Add(world);
            return world;
        }
        /// <summary>Looks up a world by its key (including the default world); returns false if not found or not booted.</summary>
        public static bool TryGetWorld(string key, out World world)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before getting worlds.");
                world = null;
                return false;
            }
            
            if (!string.IsNullOrEmpty(key))
            {
                if (_defaultWorld?.Key == key) { world = _defaultWorld; return true; }
                foreach (var w in _otherWorlds)
                    if (w.Key == key) { world = w; return true; }
            }
            world = null;
            return false;
        }
        /// <summary>Destroys and disposes an additional world; the default world cannot be destroyed and the call is rejected while iterating.</summary>
        public static bool DestroyWorld(World world)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before destroying worlds.");
                return false;
            }

            if (IsIterating)
            {
                EosLog.Error("Cannot destroy a world while the universe is iterating. Wait until the current update cycle is finished.");
                return false;
            }
            
            if (world == null || world.IsDisposed) return false;
            if (world.Equals(_defaultWorld)) return false;
            if (!_otherWorlds.Remove(world)) return false;
            world.Dispose();
            return true;
        }

        /// <summary>Runs the <see cref="UpdateType.Update"/> phase on every non-manual world.</summary>
        public static void Update(float deltaTime)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before updating.");
                return;
            }
            if (!IsEnabled) return;
            
            BeginIteration();
            try
            {
                if (_defaultWorld is { IsManualUpdate: false })
                    _defaultWorld.Update(deltaTime);

                foreach (var world in _otherWorlds)
                    if (world is { IsManualUpdate: false })
                        world.Update(deltaTime);
            }
            finally { EndIteration(); }
        }
        /// <summary>Fixed-step accumulator that calls <see cref="Update"/> zero or more times per real frame (clamped at <paramref name="maxSteps"/>, dropping any leftover backlog); <paramref name="fixedStep"/> &lt;= 0 degenerates to a single <see cref="Update"/>.</summary>
        public static void Tick(float realDelta, float fixedStep = 1f / 60f, int maxSteps = 8)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before updating.");
                return;
            }
            if (!IsEnabled) return;
            if (fixedStep <= 0f) { Update(realDelta); return; }

            _accumulator += realDelta;
            int steps = 0;
            while (_accumulator >= fixedStep && steps < maxSteps)
            {
                Update(fixedStep);
                _accumulator -= fixedStep;
                steps++;
            }
            if (_accumulator >= fixedStep) _accumulator = 0f;
        }
        /// <summary>Runs the <see cref="UpdateType.FixedUpdate"/> phase on every non-manual world.</summary>
        public static void FixedUpdate(float deltaTime)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before updating.");
                return;
            }
            if (!IsEnabled) return;
            
            BeginIteration();
            try
            {
                if (_defaultWorld is { IsManualUpdate: false })
                    _defaultWorld.FixedUpdate(deltaTime);

                foreach (var world in _otherWorlds)
                    if (world is { IsManualUpdate: false })
                        world.FixedUpdate(deltaTime);
            }
            finally { EndIteration(); }
        }
        /// <summary>Runs the <see cref="UpdateType.LateUpdate"/> phase on every non-manual world.</summary>
        public static void LateUpdate(float deltaTime)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before updating.");
                return;
            }
            if (!IsEnabled) return;
            
            BeginIteration();
            try
            {
                if (_defaultWorld is { IsManualUpdate: false })
                    _defaultWorld.LateUpdate(deltaTime);

                foreach (var world in _otherWorlds)
                    if (world is { IsManualUpdate: false })
                        world.LateUpdate(deltaTime);
            }
            finally { EndIteration(); }
        }

        /// <summary>Fans out a debug-draw (gizmo) pass to every world.</summary>
        public static void DebugDraw()
        {
            if (!IsBooted) return;
            if (!IsEnabled) return;
            _defaultWorld.DebugDraw();
            foreach (var world in _otherWorlds)
                world.DebugDraw();
        }

        /// <summary>Enables ticking; rejected while iterating.</summary>
        public static void On()
        {
            if (!IsBooted) return;
            if (IsIterating)
            {
                EosLog.Error("Cannot enable the universe while it is iterating. Wait until the current update cycle is finished.");
                return;
            }
            IsEnabled = true;
        }
        /// <summary>Disables ticking (per-frame calls become silent no-ops); rejected while iterating.</summary>
        public static void Off()
        {
            if (!IsBooted) return;
            if (IsIterating)
            {
                EosLog.Error("Cannot disable the universe while it is iterating. Wait until the current update cycle is finished.");
                return;
            }
            IsEnabled = false;
        }
    }
}