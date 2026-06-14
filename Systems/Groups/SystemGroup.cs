using EOS.Core;

namespace EOS.Systems.Groups
{
    /// <summary>Marker base for system groups; subclass and reference it via <c>[Group(typeof(MyGroup))]</c> to assign systems, nesting groups by inheritance.</summary>
    public abstract class SystemGroup
    {
        /// <summary>Enables this group in the given world.</summary>
        public void Enable(World world) => world.SystemGroups.SetEnabled(GetType(), true);
        /// <summary>Disables this group in the given world, suspending its systems and descendant groups.</summary>
        public void Disable(World world) => world.SystemGroups.SetEnabled(GetType(), false);
        /// <summary>True if this group and all its ancestors are enabled in the given world.</summary>
        public bool IsEnabled(World world) => world.SystemGroups.IsEnabled(GetType());
    }
}