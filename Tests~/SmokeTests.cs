using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using Xunit;

namespace EOS.Tests
{
    public sealed class SmokeComponent : EosObject { }

    public sealed class SmokeTests
    {
        [Fact]
        public void World_CreatesEntity_AndAddsComponent()
        {
            var world = new World();
            world.Init();

            var entity = new EosEntity(world, "smoke", active: true);

            Assert.True(entity.IsValid);
            Assert.False(entity.Has<SmokeComponent>());

            entity.Add<SmokeComponent>();

            Assert.True(entity.Has<SmokeComponent>());

            world.Dispose();
        }
    }
}
