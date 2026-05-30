using EOS.Core;

namespace EOS.Systems.Groups
{
    public abstract class SystemGroup
    {
        public void Enable(World world) => world.SystemGroups.SetEnabled(GetType(), true);
        public void Disable(World world) => world.SystemGroups.SetEnabled(GetType(), false);
        public bool IsEnabled(World world) => world.SystemGroups.IsEnabled(GetType());
    }
}