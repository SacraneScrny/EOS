namespace EOS.Objects.Interfaces
{
    /// <summary>Implement on an <see cref="EosObject"/> to receive a per-frame callback during the world's Update phase.</summary>
    public interface IObjectUpdate
    {
        /// <summary>Whether the component is currently enabled; only enabled components are updated.</summary>
        bool IsEnabled { get; }
        /// <summary>Called once per Update phase with the frame delta time.</summary>
        void OnUpdate(float deltaTime);
    }

    /// <summary>Implement on an <see cref="EosObject"/> to receive a per-frame callback during the world's FixedUpdate phase.</summary>
    public interface IObjectFixedUpdate
    {
        /// <summary>Whether the component is currently enabled; only enabled components are updated.</summary>
        bool IsEnabled { get; }
        /// <summary>Called once per FixedUpdate phase with the fixed-step delta time.</summary>
        void OnFixedUpdate(float deltaTime);
    }

    /// <summary>Implement on an <see cref="EosObject"/> to receive a per-frame callback during the world's LateUpdate phase.</summary>
    public interface IObjectLateUpdate
    {
        /// <summary>Whether the component is currently enabled; only enabled components are updated.</summary>
        bool IsEnabled { get; }
        /// <summary>Called once per LateUpdate phase with the frame delta time.</summary>
        void OnLateUpdate(float deltaTime);
    }
}
