using EOS.Core;

namespace EOS.Systems
{
    public abstract class EosSystem
    {
        public bool IsEnabled { get; private set; } = true;

        protected World World { get; private set; }
        internal void SetWorld(World world) => World = world;

        protected LocalSystemContext Context => new(World.LocalContext, this);

        public virtual UpdateType UpdateType => UpdateType.Update;

        public bool IsUpdate() => IsEnabled && UpdateWhen();
        protected virtual bool UpdateWhen() => true;

        public virtual void Awake() { }
        public virtual void Start() { }
    }
}
