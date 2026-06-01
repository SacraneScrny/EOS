using System;

using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Loader;
using EOS.Objects;
using EOS.Serialization;
using Xunit;

namespace EOS.Tests
{
    public sealed class SerComp : EosObject, IObjectSerializable
    {
        public int Hp;
        public Type DataType => typeof(int);
        public object SerializeData() => Hp;
        public void DeserializeData(object data, IDeserializeContext ctx) => Hp = (int)data;
    }

    public sealed class MyView { }

    public sealed class SerializationTests
    {
        [Fact]
        public void RoundTrip_RestoresEntitiesComponentsAndClosedGenerics()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var world = Universe.InternalDefaultWorld;

            var hero = new EosEntity(world, "hero", true);
            world.Entities.SetStableKey(hero, "hero");
            hero.Add<SerComp>().Hp = 42;
            hero.Add<CompA>();
            hero.Add<Incarnation<MyView>>().Setup("avatar");

            var snapshot = WorldSerializer.Capture();

            Universe.Boot();
            WorldSerializer.Restore(snapshot);
            var restored = Universe.InternalDefaultWorld;

            Assert.True(restored.Entities.TryFind("hero", out var hero2));
            Assert.Equal(42, hero2.Get<SerComp>().Hp);
            Assert.True(hero2.Has<CompA>());
            Assert.Equal("avatar", hero2.Get<Incarnation<MyView>>().Id);
        }

        [Fact]
        public void Capture_SkipsNonSerializableEntities()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var world = Universe.InternalDefaultWorld;

            var keep = new EosEntity(world, "keep", true);
            world.Entities.SetStableKey(keep, "keep");
            keep.Add<SerComp>().Hp = 1;

            var ghost = new EosEntity(world, "ghost", true, isSerializable: false);
            ghost.Add<SerComp>().Hp = 99;

            var snapshot = WorldSerializer.Capture();

            Universe.Boot();
            WorldSerializer.Restore(snapshot);
            var restored = Universe.InternalDefaultWorld;

            Assert.Equal(1, restored.Entities.AliveCount);
            Assert.True(restored.Entities.TryFind("keep", out _));
        }

        [Fact]
        public void RoundTrip_RecreatesKeyedWorld()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var level = Universe.CreateWorld("level1");

            var block = new EosEntity(level, "block", true);
            level.Entities.SetStableKey(block, "block");
            block.Add<SerComp>().Hp = 9;

            var snapshot = WorldSerializer.Capture();

            Universe.Boot();
            WorldSerializer.Restore(snapshot);

            Assert.True(Universe.TryGetWorld("level1", out var level2));
            Assert.True(level2.Entities.TryFind("block", out var block2));
            Assert.Equal(9, block2.Get<SerComp>().Hp);
        }

        [Fact]
        public void Capture_SkipsNonSerializableWorld()
        {
            EosDomainReset.Reset();
            Universe.Boot();
            var hidden = Universe.CreateWorld("hidden", isSerializable: false);
            new EosEntity(hidden, "x", true);

            var snapshot = WorldSerializer.Capture();

            Assert.DoesNotContain(snapshot.Worlds, ws => ws.WorldKey == "hidden");
        }
    }
}
