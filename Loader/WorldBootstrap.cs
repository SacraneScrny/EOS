using System;
using EOS.Core;
using EOS.Logging;

namespace EOS.Loader
{
    public static class WorldBootstrap
    {
        public static Action<World> Provider;

        internal static void Apply(World world)
        {
            if (world == null) return;
            var provider = Provider;
            if (provider == null) return;
            try
            {
                provider(world);
            }
            catch (Exception ex)
            {
                EosLog.Error($"world bootstrap threw: {ex.Message}", nameof(WorldBootstrap));
            }
        }
    }
}
