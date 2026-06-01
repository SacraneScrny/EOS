using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using Xunit;

namespace EOS.Tests
{
    public sealed class EntitiesContainerTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void Create_ProducesValidEntity()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);

            Assert.True(e.IsValid);
            Assert.Equal(1, world.Entities.AliveCount);
        }

        [Fact]
        public void Destroy_InvalidatesEntity_AndBumpsVersion()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);

            e.Destroy();

            Assert.False(e.IsValid);
            Assert.Equal(0, world.Entities.AliveCount);
        }

        [Fact]
        public void Destroy_ThenCreate_ReusesIdWithNewVersion()
        {
            var world = NewWorld();
            var a = new EosEntity(world, "a", true);
            int reusedId = a.Id;
            a.Destroy();

            var b = new EosEntity(world, "b", true);

            Assert.Equal(reusedId, b.Id);
            Assert.NotEqual(a.Version, b.Version);
            Assert.False(a.IsValid);
            Assert.True(b.IsValid);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void StableKey_RoundTrips_AndClearsOnDestroy()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);

            world.Entities.SetStableKey(e, "hero");

            Assert.True(world.Entities.TryFind("hero", out var found));
            Assert.Equal(e, found);
            Assert.Equal("hero", world.Entities.GetStableKey(e));

            e.Destroy();

            Assert.False(world.Entities.TryFind("hero", out _));
        }

        [Fact]
        public void StableKey_Reassignment_DropsOldKey()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);

            world.Entities.SetStableKey(e, "k1");
            world.Entities.SetStableKey(e, "k2");

            Assert.False(world.Entities.TryFind("k1", out _));
            Assert.True(world.Entities.TryFind("k2", out _));
        }

        [Fact]
        public void Active_TogglesViaExtensions()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", active: false);

            Assert.False(e.IsActive);
            e.On();
            Assert.True(e.IsActive);
            e.Off();
            Assert.False(e.IsActive);
        }

        [Fact]
        public void Create_GrowsCapacity_BeyondInitial()
        {
            var world = NewWorld();
            var entities = new List<EosEntity>();
            for (int i = 0; i < 1100; i++)
                entities.Add(new EosEntity(world, "e" + i, true));

            Assert.Equal(1100, world.Entities.AliveCount);
            Assert.All(entities, e => Assert.True(e.IsValid));
        }

        [Fact]
        public void All_EnumeratesAliveEntities()
        {
            var world = NewWorld();
            new EosEntity(world, "a", true);
            new EosEntity(world, "b", true);
            new EosEntity(world, "c", true);

            int count = 0;
            foreach (var _ in world.Entities.All()) count++;

            Assert.Equal(3, count);
        }

        [Fact]
        public void Reset_ClearsAllEntities()
        {
            var world = NewWorld();
            new EosEntity(world, "a", true);
            new EosEntity(world, "b", true);

            world.Reset();

            Assert.Equal(0, world.Entities.AliveCount);
        }
    }
}
