using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Systems;
using Xunit;

namespace EOS.Tests
{
    public sealed class WithTagProbe : EosObject { public int Calls; }
    public sealed class WithoutTagProbe : EosObject { public int Calls; }
    public sealed class AnyTagProbe : EosObject { public int Calls; }
    public sealed class OneTagProbe : EosObject { public int Calls; }

    public sealed class WithTagSystem : EosSystem
    {
        [WithTag("active")]
        void Execute(WithTagProbe p) => p.Calls++;
    }

    public sealed class WithoutTagSystem : EosSystem
    {
        [WithoutTag("frozen")]
        void Execute(WithoutTagProbe p) => p.Calls++;
    }

    public sealed class WithAnyTagSystem : EosSystem
    {
        [WithAnyTag("fire", "ice")]
        void Execute(AnyTagProbe p) => p.Calls++;
    }

    public sealed class WithOneTagSystem : EosSystem
    {
        [WithOneTag("a", "b")]
        void Execute(OneTagProbe p) => p.Calls++;
    }

    public sealed class SystemTagFilterTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void WithTag_RequiresTagPresent()
        {
            var world = NewWorld();
            var untagged = new EosEntity(world, "untagged", true);
            untagged.Add<WithTagProbe>();
            var tagged = new EosEntity(world, "tagged", true);
            tagged.Add<WithTagProbe>();
            tagged.AddTag("active");

            world.Update(0f);

            Assert.Equal(0, untagged.Get<WithTagProbe>().Calls);
            Assert.Equal(1, tagged.Get<WithTagProbe>().Calls);
        }

        [Fact]
        public void WithoutTag_RejectsTaggedEntities()
        {
            var world = NewWorld();
            var clean = new EosEntity(world, "clean", true);
            clean.Add<WithoutTagProbe>();
            var frozen = new EosEntity(world, "frozen", true);
            frozen.Add<WithoutTagProbe>();
            frozen.AddTag("frozen");

            world.Update(0f);

            Assert.Equal(1, clean.Get<WithoutTagProbe>().Calls);
            Assert.Equal(0, frozen.Get<WithoutTagProbe>().Calls);
        }

        [Fact]
        public void WithAnyTag_RequiresAtLeastOne()
        {
            var world = NewWorld();
            var none = new EosEntity(world, "none", true);
            none.Add<AnyTagProbe>();
            var fire = new EosEntity(world, "fire", true);
            fire.Add<AnyTagProbe>();
            fire.AddTag("fire");

            world.Update(0f);

            Assert.Equal(0, none.Get<AnyTagProbe>().Calls);
            Assert.Equal(1, fire.Get<AnyTagProbe>().Calls);
        }

        [Fact]
        public void WithOneTag_RequiresExactlyOne()
        {
            var world = NewWorld();
            var one = new EosEntity(world, "one", true);
            one.Add<OneTagProbe>();
            one.AddTag("a");
            var both = new EosEntity(world, "both", true);
            both.Add<OneTagProbe>();
            both.AddTag("a", "b");

            world.Update(0f);

            Assert.Equal(1, one.Get<OneTagProbe>().Calls);
            Assert.Equal(0, both.Get<OneTagProbe>().Calls);
        }
    }
}
