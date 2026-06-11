using EOS.Core;

namespace EOS.Systems
{
    public abstract class EosSystem
    {
        public bool IsEnabled { get; private set; } = true;

        protected World World { get; private set; }
        internal void SetWorld(World world) => World = world;

        protected LocalSystemContext Context => new(World.LocalContext, this);

        protected IServiceLocator Services => World.Services;

        public virtual UpdateType UpdateType => UpdateType.Update;

        public bool IsUpdate() => IsEnabled && UpdateWhen();
        protected virtual bool UpdateWhen() => true;

        public virtual void Awake() { }
        public virtual void Start() { }
        public virtual void OnDebugDraw() { }

        public void On()
        {
            if (IsEnabled) return;
            IsEnabled = true;
            OnEnable();
        }
        protected virtual void OnEnable() { }
        
        public void Off()
        {
            if (!IsEnabled) return;
            IsEnabled = false;
            OnDisable();
        }
        public virtual void OnDisable() { }
    }
}
