using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using Xunit;

namespace EOS.Tests
{
    public sealed class FrameLoopTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void Update_AdvancesFrameCounter()
        {
            var world = NewWorld();
            ulong before = world.Frame;

            world.Update(0f);

            Assert.Equal(before + 1, world.Frame);
        }

        [Fact]
        public void AfterUpdateBuffer_CreatesEntityDuringFrame()
        {
            var world = NewWorld();

            var deferred = world.AfterUpdate.Create("spawned");
            Assert.False(deferred.IsResolved);

            world.Update(0f);

            Assert.True(deferred.IsResolved);
            Assert.True(deferred.Value.IsValid);
        }

        [Fact]
        public void BeforeUpdateBuffer_AddsComponentDuringFrame()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);

            world.BeforeUpdate.Schedule(e).Add<CompA>();
            Assert.False(e.Has<CompA>());

            world.Update(0f);

            Assert.True(e.Has<CompA>());
        }

        [Fact]
        public void DisabledWorld_DoesNotAdvanceFrame()
        {
            var world = NewWorld();
            world.Reset();
            ulong before = world.Frame;

            world.Update(0f);

            Assert.Equal(before, world.Frame);
        }
    }
}
