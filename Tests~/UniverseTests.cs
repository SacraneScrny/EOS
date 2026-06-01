using EOS.Core;
using EOS.Loader;
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
    }
}
