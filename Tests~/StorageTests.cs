using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Storage;
using Xunit;

namespace EOS.Tests
{
    public sealed class StorageTests
    {
        static (World world, Storage<CompA> storage) NewStorage()
        {
            var world = new World();
            world.Init();
            return (world, world.ObjectsStorages.Get<CompA>());
        }

        [Fact]
        public void Add_RegistersComponent_AndIsDiscoverable()
        {
            var (world, storage) = NewStorage();
            var e = new EosEntity(world, "e", true);

            var comp = storage.Add(e);

            Assert.NotNull(comp);
            Assert.True(storage.Has(e));
            Assert.Equal(1, storage.Count);
            Assert.True(storage.TryGet(e, out var got));
            Assert.Same(comp, got);
        }

        [Fact]
        public void Add_IsIdempotent_PerEntity()
        {
            var (world, storage) = NewStorage();
            var e = new EosEntity(world, "e", true);

            var first = storage.Add(e);
            first.Value = 7;
            var second = storage.Add(e);

            Assert.Same(first, second);
            Assert.Equal(7, second.Value);
            Assert.Equal(1, storage.Count);
        }

        [Fact]
        public void Get_OnMissingEntity_ReturnsNull()
        {
            var (world, storage) = NewStorage();
            var e = new EosEntity(world, "e", true);

            Assert.Null(storage.Get(e));
            Assert.False(storage.TryGet(e, out _));
        }

        [Fact]
        public void Remove_ClearsComponent()
        {
            var (world, storage) = NewStorage();
            var e = new EosEntity(world, "e", true);
            storage.Add(e);

            Assert.True(storage.Remove(e));
            Assert.False(storage.Has(e));
            Assert.Equal(0, storage.Count);
            Assert.False(storage.Remove(e));
        }

        [Fact]
        public void Remove_KeepsDenseArrayContiguous_ViaSwapRemove()
        {
            var (world, storage) = NewStorage();
            var e0 = new EosEntity(world, "e0", true);
            var e1 = new EosEntity(world, "e1", true);
            var e2 = new EosEntity(world, "e2", true);
            storage.Add(e0).Value = 10;
            storage.Add(e1).Value = 20;
            storage.Add(e2).Value = 30;

            Assert.True(storage.Remove(e1));

            Assert.Equal(2, storage.Count);
            Assert.False(storage.Has(e1));
            Assert.Equal(10, storage.Get(e0).Value);
            Assert.Equal(30, storage.Get(e2).Value);
        }

        [Fact]
        public void IndexOf_RejectsStaleHandle_AfterDestroy()
        {
            var (world, storage) = NewStorage();
            var e = new EosEntity(world, "e", true);
            storage.Add(e);

            e.Destroy();

            Assert.Equal(-1, storage.IndexOf(e));
            Assert.False(storage.Has(e));
        }

        [Fact]
        public void MarkReady_AdvancesAddVersion()
        {
            var (world, storage) = NewStorage();
            var e = new EosEntity(world, "e", true);
            storage.Add(e);

            Assert.Equal(0UL, storage.MaxAddVersion);

            storage.MarkReady(e);

            Assert.True(storage.MaxAddVersion > 0);
            Assert.Equal(storage.MaxAddVersion, storage.AddVersionAt(storage.IndexOf(e)));
        }

        [Fact]
        public void Bump_IsDedupedWithinFrame_AndAdvancesOnNewFrame()
        {
            var (world, storage) = NewStorage();
            var e = new EosEntity(world, "e", true);
            storage.Add(e);

            storage.Bump(e);
            var afterFirst = storage.MaxMarkVersion;
            Assert.True(afterFirst > 0);

            storage.Bump(e);
            Assert.Equal(afterFirst, storage.MaxMarkVersion);

            world.NextFrame();
            storage.Bump(e);
            Assert.True(storage.MaxMarkVersion > afterFirst);
        }

        [Fact]
        public void Clear_ResetsCountAndWatermarks()
        {
            var (world, storage) = NewStorage();
            var e = new EosEntity(world, "e", true);
            storage.Add(e);
            storage.MarkReady(e);
            storage.Bump(e);

            storage.Clear();

            Assert.Equal(0, storage.Count);
            Assert.Equal(0UL, storage.MaxAddVersion);
            Assert.Equal(0UL, storage.MaxMarkVersion);
        }

        [Fact]
        public void Add_GrowsBackingArrays_BeyondInitialCapacity()
        {
            var (world, storage) = NewStorage();
            var entities = new List<EosEntity>();
            for (int i = 0; i < 1100; i++)
            {
                var e = new EosEntity(world, "e" + i, true);
                storage.Add(e).Value = i;
                entities.Add(e);
            }

            Assert.Equal(1100, storage.Count);
            Assert.Equal(0, storage.Get(entities[0]).Value);
            Assert.Equal(1099, storage.Get(entities[1099]).Value);
        }
    }
}
