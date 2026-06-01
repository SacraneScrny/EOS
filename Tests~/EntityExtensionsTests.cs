using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using Xunit;

namespace EOS.Tests
{
    public sealed class EntityExtensionsTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void DestroyedEntity_ComponentOpsAreSafeNoops()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<CompA>();
            e.Destroy();

            Assert.False(e.Has<CompA>());
            Assert.False(e.Remove<CompA>());
            Assert.False(e.TryGet<CompA>(out _));
            e.Bump<CompA>();
        }

        [Fact]
        public void NullEntity_QueriesReturnFalse()
        {
            Assert.False(EosEntity.Null.Has<CompA>());
            Assert.False(EosEntity.Null.TryGet<CompA>(out _));
            Assert.False(EosEntity.Null.HasTag("x"));
        }

        [Fact]
        public void InvalidEntity_TagOpsAreSafeNoops()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Destroy();

            e.AddTag("x");
            e.RemoveTag("x");
            e.SetFlag("y", true);
            e.ClearTags();
            e.On();
            e.Off();

            Assert.False(e.HasTag("x"));
        }

        [Fact]
        public void HasAnyIn_FlagsEnum_DetectsMember()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);

            Assert.False(e.HasAnyIn<Perm>());

            e.AddTag(Perm.Read);

            Assert.True(e.HasAnyIn<Perm>());
        }
    }
}
