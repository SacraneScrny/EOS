using EOS.Core;
using EOS.Entities;
using Xunit;

namespace EOS.Tests
{
    public sealed class EosEntityTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void Null_IsInvalidAndHasNegativeId()
        {
            Assert.Equal(-1, EosEntity.Null.Id);
            Assert.False(EosEntity.Null.IsValid);
        }

        [Fact]
        public void TwoHandlesToSameEntity_AreEqual()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            var same = new EosEntity(e.Id, e.Version, world, e.Name);

            Assert.True(e == same);
            Assert.Equal(e, same);
            Assert.Equal(e.GetHashCode(), same.GetHashCode());
        }

        [Fact]
        public void ImplicitConversion_YieldsId()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);

            int id = e;

            Assert.Equal(e.Id, id);
        }

        [Fact]
        public void ReusedId_WithNewVersion_IsNotEqualToStaleHandle()
        {
            var world = NewWorld();
            var a = new EosEntity(world, "a", true);
            a.Destroy();
            var b = new EosEntity(world, "b", true);

            Assert.Equal(a.Id, b.Id);
            Assert.True(a != b);
        }
    }
}
