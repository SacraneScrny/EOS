using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Systems;
using Xunit;

namespace EOS.Tests
{
    public sealed class NewProbe : EosObject { public int NewCount; public int EveryCount; }
    public sealed class BumpProbe : EosObject { public int BumpCount; }

    public sealed class NewReactiveSystem : EosSystem
    {
        void Execute([New] NewProbe p) => p.NewCount++;
    }

    public sealed class EveryFrameSystem : EosSystem
    {
        void Execute(NewProbe p) => p.EveryCount++;
    }

    public sealed class BumpReactiveSystem : EosSystem
    {
        void Execute([Bumped] BumpProbe p) => p.BumpCount++;
    }

    public sealed class SystemReactiveTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void NewChannel_FiresOnceAfterAdd()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<NewProbe>();

            world.Update(0f);
            world.Update(0f);

            var p = e.Get<NewProbe>();
            Assert.Equal(1, p.NewCount);
            Assert.Equal(2, p.EveryCount);
        }

        [Fact]
        public void BumpedChannel_FiresOnlyInFrameAfterBump()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<BumpProbe>();

            world.Update(0f);
            Assert.Equal(0, e.Get<BumpProbe>().BumpCount);

            e.Bump<BumpProbe>();
            world.Update(0f);
            Assert.Equal(1, e.Get<BumpProbe>().BumpCount);

            world.Update(0f);
            Assert.Equal(1, e.Get<BumpProbe>().BumpCount);
        }
    }
}
