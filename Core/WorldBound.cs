namespace EOS.Core
{
    public abstract class WorldBound
    {
        protected World World { get; private set; }
        internal void Init(World world)
        {
            World = world;
            OnInited();
        }
        protected virtual void OnInited() { }
    }
}