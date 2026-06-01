using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Systems;
using Xunit;

namespace EOS.Tests
{
    public sealed class GuardProbe : EosObject { }

    public sealed class StructuralChangeSystem : EosSystem
    {
        void Execute(GuardProbe p, EosEntity e) => e.Add<CompA>();
    }

    public sealed class StructuralChangePolicyTests
    {
        static World NewWorld(StructuralChangePolicy policy)
        {
            var world = new World { StructuralChangePolicy = policy };
            world.Init();
            return world;
        }

        [Fact]
        public void Throw_BlocksStructuralChangeDuringIteration()
        {
            var world = NewWorld(StructuralChangePolicy.Throw);
            var e = new EosEntity(world, "e", true);
            e.Add<GuardProbe>();

            world.Update(0f);

            Assert.False(e.Has<CompA>());
        }

        [Fact]
        public void Allow_PermitsStructuralChangeDuringIteration()
        {
            var world = NewWorld(StructuralChangePolicy.Allow);
            var e = new EosEntity(world, "e", true);
            e.Add<GuardProbe>();

            world.Update(0f);

            Assert.True(e.Has<CompA>());
        }

        [Fact]
        public void Warn_PermitsStructuralChangeDuringIteration()
        {
            var world = NewWorld(StructuralChangePolicy.Warn);
            var e = new EosEntity(world, "e", true);
            e.Add<GuardProbe>();

            world.Update(0f);

            Assert.True(e.Has<CompA>());
        }

        [Fact]
        public void Throw_AllowsStructuralChangeOutsideIteration()
        {
            var world = NewWorld(StructuralChangePolicy.Throw);
            var e = new EosEntity(world, "e", true);

            e.Add<CompA>();

            Assert.True(e.Has<CompA>());
        }
    }
}
