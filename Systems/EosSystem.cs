using EOS.Core;

namespace EOS.Systems
{
    /// <summary>Base class for all systems; declare an <c>Execute(...)</c> and/or <c>EventExecute(T)</c> method whose parameter types define the query, and the runner discovers and drives it each frame.</summary>
    public abstract class EosSystem
    {
        /// <summary>Whether the system runs; toggled by <see cref="On"/>/<see cref="Off"/>.</summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>The owning world, available once the runner has wired the system.</summary>
        protected World World { get; private set; }
        internal void SetWorld(World world) => World = world;

        /// <summary>Per-system view of the world blackboard, adding change-watermark helpers (<c>Changed&lt;T&gt;</c>).</summary>
        protected LocalSystemContext Context => new(World.LocalContext, this);

        /// <summary>The world service locator.</summary>
        protected IServiceLocator Services => World.Services;

        /// <summary>Which phase this system runs in; override to route it to <c>FixedUpdate</c> or <c>LateUpdate</c> (default <c>Update</c>).</summary>
        public virtual UpdateType UpdateType => UpdateType.Update;

        /// <summary>True when the system should run this frame: enabled and <see cref="UpdateWhen"/> returns true.</summary>
        public bool IsUpdate() => IsEnabled && UpdateWhen();
        /// <summary>Override to gate the system at runtime per frame; return false to skip without disabling.</summary>
        protected virtual bool UpdateWhen() => true;

        /// <summary>One-time lifecycle hook run when the system is created, before <see cref="Start"/>.</summary>
        public virtual void Awake() { }
        /// <summary>One-time lifecycle hook run after every system has been awoken.</summary>
        public virtual void Start() { }
        /// <summary>Override to draw debug gizmos; invoked during the universe debug-draw pass.</summary>
        public virtual void OnDebugDraw() { }

        /// <summary>Enables the system (no-op if already enabled), firing <see cref="OnEnable"/>.</summary>
        public void On()
        {
            if (IsEnabled) return;
            IsEnabled = true;
            OnEnable();
        }
        /// <summary>Called when the system transitions to enabled.</summary>
        protected virtual void OnEnable() { }

        /// <summary>Disables the system (no-op if already disabled), firing <see cref="OnDisable"/>.</summary>
        public void Off()
        {
            if (!IsEnabled) return;
            IsEnabled = false;
            OnDisable();
        }
        /// <summary>Called when the system transitions to disabled.</summary>
        public virtual void OnDisable() { }
    }
}
