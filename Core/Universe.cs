using System.Collections.Generic;

namespace EOS.Core
{
    public static class Universe
    {
        public static World DefaultWorld { get; private set; }
        
        static readonly List<World> _otherWorlds = new List<World>();
        public static IReadOnlyList<World> OtherWorlds => _otherWorlds;
        
        public static void Boot()
        {
            DefaultWorld = new();
            DefaultWorld.Init();  
        }
        public static void Reset()
        {
            DefaultWorld.Reset();
            foreach (var world in _otherWorlds) world.Reset();
        }

        public static World CreateWorld()
        {
            var world = new World();
            world.Init();
            _otherWorlds.Add(world);
            return world;
        }
        public static bool DestroyWorld(World world)
        {
            if (world == null || world.IsDisposed) return false;
            if (world == DefaultWorld) return false;
            if (!_otherWorlds.Remove(world)) return false;
            world.Dispose();
            return true;
        }
    }
}