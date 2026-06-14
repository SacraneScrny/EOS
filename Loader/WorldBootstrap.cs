using System;
using EOS.Core;
using EOS.Logging;

namespace EOS.Loader
{
    /// <summary>Per-world seeding seam: the consumer assigns <see cref="Provider"/> and it runs for every world on <c>Init</c> and <c>Reset</c>.</summary>
    public static class WorldBootstrap
    {
        /// <summary>Optional callback invoked for each world during init/reset to seed context, services and entities; null means no-op.</summary>
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
