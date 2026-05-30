using EOS.Core;

namespace EOS.Systems
{
    public abstract class EosSystem
    {
        public bool IsEnabled { get; private set; } = true;

        public virtual UpdateType UpdateType => UpdateType.Update;

        public bool IsUpdate() => IsEnabled && UpdateWhen();
        protected virtual bool UpdateWhen() => true;

        public virtual void Awake() { }
        public virtual void Start() { }
    }
}
