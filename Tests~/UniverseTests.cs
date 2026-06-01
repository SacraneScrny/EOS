using EOS.Core;
using EOS.Entities;
using EOS.Loader;
using EOS.Serialization;
using Xunit;

namespace EOS.Tests
{
    public sealed class UniverseTests
    {
        [Fact]
        public void Boot_InitializesEnabledDefaultWorld()
        {
            EosDomainReset.Reset();
            Universe.Boot();

            Assert.True(Universe.IsEnabled);
            Assert.NotNull(Universe.DefaultWorld);
            Assert.Equal(1, Universe.TotalWorldsCount);
        }

        [Fact]
        public void CreateWorld_AddsAndResolvesByKey()
        {
            EosDomainReset.Reset();
            Universe.Boot();

            var extra = Universe.CreateWorld("extra");

            Assert.Equal(2, Universe.TotalWorldsCount);
            Assert.True(Universe.TryGetWorld("extra", out var found));
            Assert.Same(extra, found);
        }

        [Fact]
        public void DestroyWorld_RemovesNonDefault_RefusesDefault()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var extra = Universe.CreateWorld("extra");

            Assert.True(Universe.DestroyWorld(extra));
            Assert.True(extra.IsDisposed);
            Assert.Equal(1, Universe.TotalWorldsCount);

            Assert.False(Universe.DestroyWorld(Universe.InternalDefaultWorld));
        }

        [Fact]
        public void Off_GatesUpdates_OnResumes()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var world = Universe.InternalDefaultWorld;

            Universe.Off();
            ulong before = world.Frame;
            Universe.Update(0f);
            Assert.Equal(before, world.Frame);

            Universe.On();
            Universe.Update(0f);
            Assert.Equal(before + 1, world.Frame);
        }

        [Fact]
        public void ManualUpdateWorld_SkippedByUniverse_DrivenDirectly()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var world = Universe.InternalDefaultWorld;
            world.IsManualUpdate = true;

            ulong before = world.Frame;
            Universe.Update(0f);
            Assert.Equal(before, world.Frame);

            world.Update(0f);
            Assert.Equal(before + 1, world.Frame);
        }

        [Fact]
        public void FixedAndLateUpdate_RouteToWorlds()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var world = Universe.InternalDefaultWorld;

            ulong before = world.Frame;
            Universe.FixedUpdate(0f);
            Universe.LateUpdate(0f);

            Assert.Equal(before + 2, world.Frame);
        }

        [Fact]
        public void Reset_ClearsWorldEntities()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var world = Universe.InternalDefaultWorld;
            new EosEntity(world, "e", true);

            Universe.Reset();

            Assert.Equal(0, world.Entities.AliveCount);
        }

        [Fact]
        public void Boot_RestoresSnapshotFromOnLoad()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var seeded = new EosEntity(Universe.InternalDefaultWorld, "seed", true);
            Universe.InternalDefaultWorld.Entities.SetStableKey(seeded, "seed");
            var snapshot = WorldSerializer.Capture();

            WorldLoader.OnLoad = () => snapshot;
            try
            {
                Universe.Boot();
                Assert.True(Universe.InternalDefaultWorld.Entities.TryFind("seed", out _));
            }
            finally { WorldLoader.Reset(); }
        }
    }
}
