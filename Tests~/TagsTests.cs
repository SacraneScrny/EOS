using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using Xunit;

namespace EOS.Tests
{
    public sealed class TagsTests
    {
        static (World world, EosEntity entity) NewTagged()
        {
            var world = new World();
            world.Init();
            return (world, new EosEntity(world, "e", true));
        }

        [Fact]
        public void AddTag_String_IsObservable()
        {
            var (_, e) = NewTagged();

            e.AddTag("player");

            Assert.True(e.HasTag("player"));
            Assert.False(e.HasTag("enemy"));
        }

        [Fact]
        public void RemoveTag_ClearsBit()
        {
            var (_, e) = NewTagged();
            e.AddTag("player");

            e.RemoveTag("player");

            Assert.False(e.HasTag("player"));
        }

        [Fact]
        public void HasAllTags_RequiresEveryTag()
        {
            var (_, e) = NewTagged();
            e.AddTag("a", "b");

            Assert.True(e.HasAllTags("a", "b"));
            Assert.False(e.HasAllTags("a", "b", "c"));
        }

        [Fact]
        public void HasAnyTag_RequiresAtLeastOne()
        {
            var (_, e) = NewTagged();
            e.AddTag("a");

            Assert.True(e.HasAnyTag("a", "z"));
            Assert.False(e.HasAnyTag("x", "z"));
        }

        [Fact]
        public void HasOneTag_RequiresExactlyOne()
        {
            var (_, e) = NewTagged();
            e.AddTag("a");

            Assert.True(e.HasOneTag("a", "b"));

            e.AddTag("b");
            Assert.False(e.HasOneTag("a", "b"));
        }

        [Fact]
        public void EnumTag_IsObservable()
        {
            var (_, e) = NewTagged();

            e.AddTag(Color.Red);

            Assert.True(e.HasTag(Color.Red));
            Assert.False(e.HasTag(Color.Blue));
        }

        [Fact]
        public void FlagsEnumTag_SetsEachFlagBit()
        {
            var (_, e) = NewTagged();

            e.AddTag(Perm.Read | Perm.Write);

            Assert.True(e.HasTag(Perm.Read));
            Assert.True(e.HasTag(Perm.Write));
            Assert.False(e.HasTag(Perm.Exec));
            Assert.True(e.HasTag(Perm.Read | Perm.Write));
        }

        [Fact]
        public void HasAnyIn_DetectsAnyMemberOfEnum()
        {
            var (_, e) = NewTagged();

            Assert.False(e.HasAnyIn<Color>());

            e.AddTag(Color.Green);

            Assert.True(e.HasAnyIn<Color>());
        }

        [Fact]
        public void ClearTags_RemovesEverything()
        {
            var (_, e) = NewTagged();
            e.AddTag("a", "b");
            e.AddTag(Color.Red);

            e.ClearTags();

            Assert.False(e.HasTag("a"));
            Assert.False(e.HasTag("b"));
            Assert.False(e.HasTag(Color.Red));
        }

        [Fact]
        public void ManyDistinctTags_GrowBitsetWords()
        {
            var (_, e) = NewTagged();
            for (int i = 0; i < 70; i++)
                e.AddTag("tag" + i);

            Assert.True(e.HasTag("tag0"));
            Assert.True(e.HasTag("tag69"));
            Assert.False(e.HasTag("tag70"));
        }
    }
}
