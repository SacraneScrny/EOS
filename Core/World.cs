using EOS.Objects;
using EOS.Systems;
using EOS.Systems.CommandBuffer;

namespace EOS.Core
{
    public static class World
    {
        public static bool IsEnabled { get; private set; }

        public static EntityCommandBuffer BeforeAll { get; } = new();
        public static EntityCommandBuffer BeforeUpdate { get; } = new();
        public static EntityCommandBuffer AfterUpdate { get; } = new();
        public static EntityCommandBuffer BeforeFixedUpdate { get; } = new();
        public static EntityCommandBuffer AfterFixedUpdate { get; } = new();
        public static EntityCommandBuffer BeforeLateUpdate { get; } = new();
        public static EntityCommandBuffer AfterLateUpdate { get; } = new();
        public static EntityCommandBuffer AfterAll { get; } = new();

        public static void Reset()
        {
            IsEnabled = false;
            BeforeAll.Clear();
            BeforeUpdate.Clear();
            AfterUpdate.Clear();
            BeforeFixedUpdate.Clear();
            AfterFixedUpdate.Clear();
            BeforeLateUpdate.Clear();
            AfterLateUpdate.Clear();
            AfterAll.Clear();
        }
        public static void Init() => IsEnabled = true;

        public static void Update(float deltaTime)
        {
            if (!IsEnabled) return;
            BeforeAll.Execute();
            BeforeUpdate.Execute();
            InitializeSystemRunner.Run();
            SystemsRunner.Update(deltaTime);
            ObjectsContainer.Update(deltaTime);
            AfterUpdate.Execute();
        }
        public static void FixedUpdate(float deltaTime)
        {
            if (!IsEnabled) return;
            BeforeFixedUpdate.Execute();
            SystemsRunner.FixedUpdate(deltaTime);
            ObjectsContainer.FixedUpdate(deltaTime);
            AfterFixedUpdate.Execute();
        }
        public static void LateUpdate(float deltaTime)
        {
            if (!IsEnabled) return;
            BeforeLateUpdate.Execute();
            SystemsRunner.LateUpdate(deltaTime);
            ObjectsContainer.LateUpdate(deltaTime);
            AfterLateUpdate.Execute();
            AfterAll.Execute();
        }
    }
}
