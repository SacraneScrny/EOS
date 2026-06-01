using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Systems;
using Xunit;

namespace EOS.Tests
{
    public sealed class ConcreteProbe : EosObject { public int Calls; }
    public sealed class OptionalProbe : EosObject { public int Calls; public bool SawExtra; }
    public sealed class ExtraComp : EosObject { }
    public sealed class DtProbe : EosObject { public float Dt = -1f; }
    public sealed class EntityProbe : EosObject { public int SeenId = -2; }
    public sealed class IncludeProbe : EosObject { public int Calls; }
    public sealed class ExcludeProbe : EosObject { public int Calls; }
    public sealed class MarkerComp : EosObject { }
    public sealed class ForbiddenComp : EosObject { }
    public sealed class GlobalProbe : EosObject { }

    public interface IShape { }
    public sealed class CircleShape : EosObject, IShape { }
    public sealed class SquareShape : EosObject, IShape { }

    public sealed class ConcreteSystem : EosSystem
    {
        void Execute(ConcreteProbe p) => p.Calls++;
    }

    public sealed class OptionalSystem : EosSystem
    {
        void Execute(OptionalProbe p, [Optional] ExtraComp extra)
        {
            p.Calls++;
            p.SawExtra = extra != null;
        }
    }

    public sealed class DtSystem : EosSystem
    {
        void Execute(DtProbe p, float dt) => p.Dt = dt;
    }

    public sealed class EntityParamSystem : EosSystem
    {
        void Execute(EntityProbe p, EosEntity e) => p.SeenId = e.Id;
    }

    public sealed class IncludeSystem : EosSystem
    {
        [Include(typeof(MarkerComp))]
        void Execute(IncludeProbe p) => p.Calls++;
    }

    public sealed class ExcludeSystem : EosSystem
    {
        [Exclude(typeof(ForbiddenComp))]
        void Execute(ExcludeProbe p) => p.Calls++;
    }

    public sealed class GlobalEntitySystem : EosSystem
    {
        public int Calls;
        [Include(typeof(GlobalProbe))]
        void Execute(EosEntity e) => Calls++;
    }

    public sealed class SingleShapeSystem : EosSystem
    {
        public int Calls;
        void Execute(IShape s) => Calls++;
    }

    public sealed class EachShapeSystem : EosSystem
    {
        public int Calls;
        void Execute([Each] IShape s) => Calls++;
    }

    public sealed class SystemQueryTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void ConcreteQuery_FiresForMatchingEntities()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<ConcreteProbe>();

            world.Update(0f);

            Assert.Equal(1, e.Get<ConcreteProbe>().Calls);
        }

        [Fact]
        public void OptionalParam_IsNullWhenAbsent_PresentWhenAdded()
        {
            var world = NewWorld();
            var without = new EosEntity(world, "without", true);
            without.Add<OptionalProbe>();
            var with = new EosEntity(world, "with", true);
            with.Add<OptionalProbe>();
            with.Add<ExtraComp>();

            world.Update(0f);

            Assert.Equal(1, without.Get<OptionalProbe>().Calls);
            Assert.False(without.Get<OptionalProbe>().SawExtra);
            Assert.True(with.Get<OptionalProbe>().SawExtra);
        }

        [Fact]
        public void DeltaTimeParam_IsForwarded()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<DtProbe>();

            world.Update(0.25f);

            Assert.Equal(0.25f, e.Get<DtProbe>().Dt);
        }

        [Fact]
        public void EntityParam_IsForwarded()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<EntityProbe>();

            world.Update(0f);

            Assert.Equal(e.Id, e.Get<EntityProbe>().SeenId);
        }

        [Fact]
        public void IncludeFilter_RequiresExtraComponent()
        {
            var world = NewWorld();
            var bare = new EosEntity(world, "bare", true);
            bare.Add<IncludeProbe>();
            var marked = new EosEntity(world, "marked", true);
            marked.Add<IncludeProbe>();
            marked.Add<MarkerComp>();

            world.Update(0f);

            Assert.Equal(0, bare.Get<IncludeProbe>().Calls);
            Assert.Equal(1, marked.Get<IncludeProbe>().Calls);
        }

        [Fact]
        public void ExcludeFilter_RejectsForbiddenComponent()
        {
            var world = NewWorld();
            var clean = new EosEntity(world, "clean", true);
            clean.Add<ExcludeProbe>();
            var dirty = new EosEntity(world, "dirty", true);
            dirty.Add<ExcludeProbe>();
            dirty.Add<ForbiddenComp>();

            world.Update(0f);

            Assert.Equal(1, clean.Get<ExcludeProbe>().Calls);
            Assert.Equal(0, dirty.Get<ExcludeProbe>().Calls);
        }

        [Fact]
        public void EntityOnlyQuery_FansOutAcrossFilteredEntities()
        {
            var world = NewWorld();
            new EosEntity(world, "a", true).Add<GlobalProbe>();
            new EosEntity(world, "b", true).Add<GlobalProbe>();

            world.Update(0f);

            Assert.Equal(2, world.Systems.GetSystem<GlobalEntitySystem>().Calls);
        }

        [Fact]
        public void InterfaceQuery_DedupsByEntity_EachFansOut()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<CircleShape>();
            e.Add<SquareShape>();

            world.Update(0f);

            Assert.Equal(1, world.Systems.GetSystem<SingleShapeSystem>().Calls);
            Assert.Equal(2, world.Systems.GetSystem<EachShapeSystem>().Calls);
        }
    }
}
