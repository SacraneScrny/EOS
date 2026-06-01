using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Systems.CommandBuffer;
using Xunit;

namespace EOS.Tests
{
    public sealed class CommandBufferChainTests
    {
        static (World world, EntityCommandBuffer ecb) New()
        {
            var world = new World();
            world.Init();
            return (world, new EntityCommandBuffer(world));
        }

        [Fact]
        public void AddWithConfigure_SetsFields()
        {
            var (world, ecb) = New();
            var e = new EosEntity(world, "e", true);

            ecb.Schedule(e).Add<CompA>(c => c.Value = 5);
            ecb.Execute();

            Assert.Equal(5, e.Get<CompA>().Value);
        }

        [Fact]
        public void Change_AppliesWhenPresent_NoopWhenAbsent()
        {
            var (world, ecb) = New();
            var present = new EosEntity(world, "present", true);
            present.Add<CompA>().Value = 1;
            var absent = new EosEntity(world, "absent", true);

            ecb.Schedule(present).Change<CompA>(c => c.Value = 9);
            ecb.Schedule(absent).Change<CompA>(c => c.Value = 9);
            ecb.Execute();

            Assert.Equal(9, present.Get<CompA>().Value);
            Assert.False(absent.Has<CompA>());
        }

        [Fact]
        public void ChangeOrAdd_AddsWhenAbsent_ChangesWhenPresent()
        {
            var (world, ecb) = New();
            var fresh = new EosEntity(world, "fresh", true);
            var existing = new EosEntity(world, "existing", true);
            existing.Add<CompA>().Value = 1;

            ecb.Schedule(fresh).ChangeOrAdd<CompA>(c => c.Value = 7);
            ecb.Schedule(existing).ChangeOrAdd<CompA>(c => c.Value += 10);
            ecb.Execute();

            Assert.Equal(7, fresh.Get<CompA>().Value);
            Assert.Equal(11, existing.Get<CompA>().Value);
        }

        [Fact]
        public void If_GatesRemainingOps()
        {
            var (world, ecb) = New();
            var pass = new EosEntity(world, "pass", true);
            var stop = new EosEntity(world, "stop", true);

            ecb.Schedule(pass).If(_ => true).Add<CompA>();
            ecb.Schedule(stop).If(_ => false).Add<CompA>();
            ecb.Execute();

            Assert.True(pass.Has<CompA>());
            Assert.False(stop.Has<CompA>());
        }

        [Fact]
        public void TagOps_AddRemoveSetFlagClear()
        {
            var (world, ecb) = New();
            var e = new EosEntity(world, "e", true);
            e.AddTag("stale");

            ecb.Schedule(e)
                .AddTag("alpha")
                .RemoveTag("stale")
                .SetFlag("beta", true)
                .SetFlag("gamma", false);
            ecb.Execute();

            Assert.True(e.HasTag("alpha"));
            Assert.False(e.HasTag("stale"));
            Assert.True(e.HasTag("beta"));
            Assert.False(e.HasTag("gamma"));

            ecb.Schedule(e).ClearTags();
            ecb.Execute();

            Assert.False(e.HasTag("alpha"));
            Assert.False(e.HasTag("beta"));
        }

        [Fact]
        public void WhenTag_GatesOnAllTagsPresent()
        {
            var (world, ecb) = New();
            var tagged = new EosEntity(world, "tagged", true);
            tagged.AddTag("a", "b");
            var partial = new EosEntity(world, "partial", true);
            partial.AddTag("a");

            ecb.Schedule(tagged).WhenTag("a", "b").Add<CompA>();
            ecb.Schedule(partial).WhenTag("a", "b").Add<CompA>();
            ecb.Execute();

            Assert.True(tagged.Has<CompA>());
            Assert.False(partial.Has<CompA>());
        }

        [Fact]
        public void WhenNoTag_GatesOnAbsence()
        {
            var (world, ecb) = New();
            var clean = new EosEntity(world, "clean", true);
            var dirty = new EosEntity(world, "dirty", true);
            dirty.AddTag("blocked");

            ecb.Schedule(clean).WhenNoTag("blocked").Add<CompA>();
            ecb.Schedule(dirty).WhenNoTag("blocked").Add<CompA>();
            ecb.Execute();

            Assert.True(clean.Has<CompA>());
            Assert.False(dirty.Has<CompA>());
        }

        [Fact]
        public void WhenAnyTag_And_WhenOneTag_Gates()
        {
            var (world, ecb) = New();
            var any = new EosEntity(world, "any", true);
            any.AddTag("fire");
            var one = new EosEntity(world, "one", true);
            one.AddTag("x");
            var both = new EosEntity(world, "both", true);
            both.AddTag("x", "y");

            ecb.Schedule(any).WhenAnyTag("fire", "ice").Add<CompA>();
            ecb.Schedule(one).WhenOneTag("x", "y").Add<CompB>();
            ecb.Schedule(both).WhenOneTag("x", "y").Add<CompB>();
            ecb.Execute();

            Assert.True(any.Has<CompA>());
            Assert.True(one.Has<CompB>());
            Assert.False(both.Has<CompB>());
        }

        [Fact]
        public void Apply_MergesExternalChain()
        {
            var (world, ecb) = New();
            var e = new EosEntity(world, "e", true);

            var chain = new CommandChain().Add<CompA>().AddTag("merged");
            ecb.Schedule(e).Apply(chain);
            ecb.Execute();

            Assert.True(e.Has<CompA>());
            Assert.True(e.HasTag("merged"));
        }

        [Fact]
        public void ScheduleWithExplicitChain_RunsOps()
        {
            var (world, ecb) = New();
            var e = new EosEntity(world, "e", true);

            var chain = new CommandChain().Add<CompA>();
            ecb.Schedule(e, chain);
            ecb.Execute();

            Assert.True(e.Has<CompA>());
        }

        [Fact]
        public void BoundSchedule_ChainsAcrossEntities()
        {
            var (world, ecb) = New();
            var a = new EosEntity(world, "a", true);
            var b = new EosEntity(world, "b", true);

            ecb.Schedule(a).Add<CompA>()
               .Schedule(b).Add<CompB>();
            ecb.Execute();

            Assert.True(a.Has<CompA>());
            Assert.True(b.Has<CompB>());
        }
    }
}
