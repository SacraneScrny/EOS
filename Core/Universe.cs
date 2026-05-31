using System.Collections.Generic;

namespace EOS.Core
{
    public static class Universe
    {
        static int _nextId = 0;
        public static bool IsEnabled { get; private set; }

        static World _defaultWorld;
        public static IReadOnlyWorld DefaultWorld => _defaultWorld;

        static readonly List<World> _otherWorlds = new List<World>();
        public static IReadOnlyList<IReadOnlyWorld> OtherWorlds => _otherWorlds;

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
            IsEnabled = true;
        }
        public static void Reset()
        {
            _defaultWorld.Reset();
            foreach (var world in _otherWorlds) world.Reset();
        }

        public static World CreateWorld()
        {
            var world = new World();
            world.SetId(_nextId++);
            world.Init();
            _otherWorlds.Add(world);
            return world;
        }
        public static bool DestroyWorld(World world)
        {
            if (world == null || world.IsDisposed) return false;
            if (world.Equals(_defaultWorld)) return false;
            if (!_otherWorlds.Remove(world)) return false;
            world.Dispose();
            return true;
        }

        public static void Update(float deltaTime)
        {
            if (!IsEnabled) return;
            if (!_defaultWorld.IsManualUpdate)
                _defaultWorld.Update(deltaTime);

            foreach (var world in _otherWorlds)
                if (!world.IsManualUpdate)
                    world.Update(deltaTime);
        }
        public static void FixedUpdate(float deltaTime)
        {
            if (!IsEnabled) return;
            if (!_defaultWorld.IsManualUpdate)
                _defaultWorld.FixedUpdate(deltaTime);

            foreach (var world in _otherWorlds)
                if (!world.IsManualUpdate)
                    world.FixedUpdate(deltaTime);
        }
        public static void LateUpdate(float deltaTime)
        {
            if (!IsEnabled) return;
            if (!_defaultWorld.IsManualUpdate)
                _defaultWorld.LateUpdate(deltaTime);

            foreach (var world in _otherWorlds)
                if (!world.IsManualUpdate)
                    world.LateUpdate(deltaTime);
        }

        public static void On() => IsEnabled = true;
        public static void Off() => IsEnabled = false;
    }
}