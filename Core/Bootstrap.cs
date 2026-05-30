using EOS.Entities;
using EOS.Objects;
using EOS.Storage;
using EOS.Systems;
using EOS.Systems.Groups;

namespace EOS.Core
{
    public static class Bootstrap
    {
        public static void Init()
        {
            World.Reset();
            StorageMap.Clear();
            SystemGroups.Clear();
            ObjectsContainer.Init();
            EntitiesContainer.Init();
            SystemsRunner.Init();
            World.Init();
        }
    }
}
