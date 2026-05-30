using EOS.Objects;
using EOS.Systems;

namespace EOS.Core
{
    public static class World
    {
        public static bool IsEnabled { get; private set; }

        public static void Reset() => IsEnabled = false;
        public static void Init() => IsEnabled = true;

        public static void Update(float deltaTime)
        {
            if (!IsEnabled) return;
            InitializeSystemRunner.Run();
            SystemsRunner.Update();
            ObjectsContainer.Update(deltaTime);
        }
        public static void FixedUpdate(float deltaTime)
        {
            if (!IsEnabled) return;
            SystemsRunner.FixedUpdate();
            ObjectsContainer.FixedUpdate(deltaTime);
        }
        public static void LateUpdate(float deltaTime)
        {
            if (!IsEnabled) return;
            SystemsRunner.LateUpdate();
            ObjectsContainer.LateUpdate(deltaTime);
        }
    }
}
