namespace EOS.Objects.Interfaces
{
    public interface IObjectUpdate
    {
        bool IsEnabled { get; }
        void OnUpdate(float deltaTime);
    }

    public interface IObjectFixedUpdate
    {
        bool IsEnabled { get; }
        void OnFixedUpdate(float deltaTime);
    }

    public interface IObjectLateUpdate
    {
        bool IsEnabled { get; }
        void OnLateUpdate(float deltaTime);
    }
}
