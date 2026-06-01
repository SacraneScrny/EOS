using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Loader;
using EOS.Objects;
using Xunit;

namespace EOS.Tests
{
    public sealed class FakeView
    {
        public string Id;
        public int Syncs;
        public bool Destroyed;
    }

    public sealed class FakeBinder : IIncarnationBinder<FakeView>
    {
        public FakeView Instantiate(EosEntity entity, string incarnationId)
            => new FakeView { Id = incarnationId };
        public void Destroy(EosEntity entity, FakeView view) => view.Destroyed = true;
        public void Sync(EosEntity entity, FakeView view) => view.Syncs++;
        public void SyncFixed(EosEntity entity, FakeView view) { }
        public void SyncLate(EosEntity entity, FakeView view) { }
    }

    public sealed class IncarnationTests
    {
        [Fact]
        public void Bridge_RegisterResolveUnregister()
        {
            EosDomainReset.Reset();

            Assert.Null(IncarnationBridge.Resolve<FakeView>());

            var binder = new FakeBinder();
            IncarnationBridge.Register(binder);
            Assert.Same(binder, IncarnationBridge.Resolve<FakeView>());

            IncarnationBridge.Unregister<FakeView>();
            Assert.Null(IncarnationBridge.Resolve<FakeView>());
        }

        [Fact]
        public void Incarnation_InstantiatesViewOnAwake_AndSyncsEachFrame()
        {
            EosDomainReset.Reset();
            IncarnationBridge.Register(new FakeBinder());

            var world = new World();
            world.Init();
            var e = new EosEntity(world, "avatar", true);
            var inc = e.Add<Incarnation<FakeView>>();
            inc.Setup("hero");

            world.Update(0f);

            Assert.NotNull(inc.View);
            Assert.Equal("hero", inc.View.Id);
            Assert.Equal(1, inc.View.Syncs);
        }

        [Fact]
        public void Incarnation_DestroysViewOnDispose()
        {
            EosDomainReset.Reset();
            IncarnationBridge.Register(new FakeBinder());

            var world = new World();
            world.Init();
            var e = new EosEntity(world, "avatar", true);
            var inc = e.Add<Incarnation<FakeView>>();
            inc.Setup("hero");
            world.Update(0f);

            var view = inc.View;
            e.Remove<Incarnation<FakeView>>();

            Assert.True(view.Destroyed);
        }
    }
}
