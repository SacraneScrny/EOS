namespace EOS.Core
{
    /// <summary>Base class for any per-world subsystem; receives its owning <see cref="World"/> via <c>Init</c> and exposes it through the protected <see cref="World"/> property.</summary>
    public abstract class WorldBound
    {
        /// <summary>The owning world, assigned during initialization.</summary>
        protected World World { get; private set; }
        internal void Init(World world)
        {
            World = world;
            OnInited();
        }
        /// <summary>Override to run setup once the owning <see cref="World"/> has been assigned.</summary>
        protected virtual void OnInited() { }
    }
}