using EOS.Core;
using EOS.Storage;
using Xunit;

namespace EOS.Tests
{
    public sealed class StorageMapTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void Get_CachesStorageInstance()
        {
            var world = NewWorld();

            var first = world.ObjectsStorages.Get<CircleShape>();
            var second = world.ObjectsStorages.Get<CircleShape>();

            Assert.Same(first, second);
        }

        [Fact]
        public void Get_IndexesStorageUnderImplementedInterface()
        {
            var world = NewWorld();

            var circle = world.ObjectsStorages.Get<CircleShape>();
            var byInterface = world.ObjectsStorages.GetByInterface(typeof(IShape));

            Assert.NotNull(byInterface);
            Assert.Contains((IStorage)circle, byInterface);
        }
    }
}
