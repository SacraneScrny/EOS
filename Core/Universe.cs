using System.Collections.Generic;

using EOS.Logging;
using EOS.Serialization;

namespace EOS.Core
{
    public static class Universe
    {
        static int _nextId = 0;
        public static bool IsEnabled { get; private set; }
        public static bool IsBooted { get; private set; }
        
        public static bool IsIterating { get; private set; }
        static void BeginIteration() => IsIterating = true;
        static void EndIteration() => IsIterating = false;
        
        static float _accumulator;

        static World _defaultWorld;
        public static IReadOnlyWorld DefaultWorld => _defaultWorld;
        internal static World InternalDefaultWorld => _defaultWorld;

        static readonly List<World> _otherWorlds = new List<World>();
        public static IReadOnlyList<IReadOnlyWorld> OtherWorlds => _otherWorlds;
        internal static IReadOnlyList<World> InternalOtherWorlds => _otherWorlds;

        public static int TotalWorldsCount => 1 + _otherWorlds.Count;

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
        public static void Shutdown()
        {
            _defaultWorld?.Dispose();
            foreach (var w in _otherWorlds) w?.Dispose();
            _otherWorlds.Clear();
            _nextId = 0;
            IsEnabled = false;
            IsBooted = false;
            IsIterating = false;
        }

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
            
            var world = new World();
            world.SetId(_nextId++);
            world.SetKey(key);
            world.IsSerializable = isSerializable;
            world.Init();
            _otherWorlds.Add(world);
            return world;
        }
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

        public static void Update(float deltaTime)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before updating.");
                return;
            }
            if (!IsEnabled) return;
            
            BeginIteration();
            
            if (_defaultWorld is { IsManualUpdate: false })
                _defaultWorld.Update(deltaTime);

            foreach (var world in _otherWorlds)
                if (world is { IsManualUpdate: false })
                    world.Update(deltaTime);
            
            EndIteration();
        }
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
            if (steps >= maxSteps) _accumulator = 0f;
        }
        public static void FixedUpdate(float deltaTime)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before updating.");
                return;
            }
            if (!IsEnabled) return;
            
            BeginIteration();
            
            if (_defaultWorld is { IsManualUpdate: false })
                _defaultWorld.FixedUpdate(deltaTime);

            foreach (var world in _otherWorlds)
                if (world is { IsManualUpdate: false })
                    world.FixedUpdate(deltaTime);
            
            EndIteration();
        }
        public static void LateUpdate(float deltaTime)
        {
            if (!IsBooted)
            {
                EosLog.Error("Universe is not booted. Call Universe.Boot() before updating.");
                return;
            }
            if (!IsEnabled) return;
            
            BeginIteration();
            
            if (_defaultWorld is { IsManualUpdate: false })
                _defaultWorld.LateUpdate(deltaTime);

            foreach (var world in _otherWorlds)
                if (world is { IsManualUpdate: false })
                    world.LateUpdate(deltaTime);
            
            EndIteration();
        }

        public static void DebugDraw()
        {
            if (!IsBooted) return;
            if (!IsEnabled) return;
            _defaultWorld.DebugDraw();
            foreach (var world in _otherWorlds)
                world.DebugDraw();
        }

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