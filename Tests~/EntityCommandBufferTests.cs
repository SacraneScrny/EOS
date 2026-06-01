using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Systems.CommandBuffer;
using Xunit;

namespace EOS.Tests
{
    public sealed class EntityCommandBufferTests
    {
        static (World world, EntityCommandBuffer ecb) NewBuffer()
        {
            var world = new World();
            world.Init();
            return (world, new EntityCommandBuffer(world));
        }

        [Fact]
        public void Create_ResolvesDeferredEntity_OnExecute()
        {
            var (_, ecb) = NewBuffer();

            var deferred = ecb.Create("spawned");
            Assert.False(deferred.IsResolved);

            ecb.Execute();

            Assert.True(deferred.IsResolved);
            Assert.True(deferred.Value.IsValid);
        }

        [Fact]
        public void Schedule_AddsComponent_OnExecute()
        {
            var (world, ecb) = NewBuffer();
            var e = new EosEntity(world, "e", true);

            ecb.Schedule(e).Add<CompA>();
            Assert.False(e.Has<CompA>());

            ecb.Execute();

            Assert.True(e.Has<CompA>());
        }

        [Fact]
        public void Schedule_AddsComponentToDeferredEntity()
        {
            var (_, ecb) = NewBuffer();

            var deferred = ecb.Create("spawned");
            ecb.Schedule(deferred).Add<CompA>();

            ecb.Execute();

            Assert.True(deferred.Value.Has<CompA>());
        }

        [Fact]
        public void Schedule_Remove_DropsComponent()
        {
            var (world, ecb) = NewBuffer();
            var e = new EosEntity(world, "e", true);
            e.Add<CompA>();

            ecb.Schedule(e).Remove<CompA>();
            ecb.Execute();

            Assert.False(e.Has<CompA>());
        }

        [Fact]
        public void Schedule_Destroy_InvalidatesEntity()
        {
            var (world, ecb) = NewBuffer();
            var e = new EosEntity(world, "e", true);

            ecb.Schedule(e).Destroy();
            ecb.Execute();

            Assert.False(e.IsValid);
        }

        [Fact]
        public void Schedule_When_GateStopsChain_WhenComponentMissing()
        {
            var (world, ecb) = NewBuffer();
            var e = new EosEntity(world, "e", true);

            ecb.Schedule(e).When<CompA>().Add<CompB>();
            ecb.Execute();

            Assert.False(e.Has<CompB>());
        }

        [Fact]
        public void Schedule_When_GatePassesChain_WhenComponentPresent()
        {
            var (world, ecb) = NewBuffer();
            var e = new EosEntity(world, "e", true);
            e.Add<CompA>();

            ecb.Schedule(e).When<CompA>().Add<CompB>();
            ecb.Execute();

            Assert.True(e.Has<CompB>());
        }

        [Fact]
        public void Clear_DiscardsPendingWork()
        {
            var (world, ecb) = NewBuffer();
            var e = new EosEntity(world, "e", true);

            var deferred = ecb.Create("spawned");
            ecb.Schedule(e).Add<CompA>();

            ecb.Clear();
            ecb.Execute();

            Assert.False(deferred.IsResolved);
            Assert.False(e.Has<CompA>());
        }

        [Fact]
        public void Execute_IsIdempotent_BuffersClearedAfterRun()
        {
            var (world, ecb) = NewBuffer();
            var e = new EosEntity(world, "e", true);

            ecb.Schedule(e).Add<CompA>();
            ecb.Execute();
            e.Remove<CompA>();

            ecb.Execute();

            Assert.False(e.Has<CompA>());
        }
    }
}
