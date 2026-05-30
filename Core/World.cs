using System;

using EOS.Entities;
using EOS.Objects;
using EOS.Storage;
using EOS.Systems;
using EOS.Systems.CommandBuffer;
using EOS.Systems.Groups;

namespace EOS.Core
{
    public class World : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public bool IsEnabled { get; private set; }
        
        public EntitiesContainer Entities { get; } = new();
        public ObjectsContainer Objects { get; } = new();
        public SystemsRunner Systems { get; } = new();
        
        public ObjectsStorageMap ObjectsStorages { get; } = new();
        public SystemGroups SystemGroups { get; } = new();
        public InitializeSystemRunner InitializeSystems { get; } = new();

        #region ECB
        EntityCommandBuffer _beforeAll;
        public IReadOnlyEntityCommandBuffer BeforeAll => _beforeAll;
        
        EntityCommandBuffer _beforeUpdate;
        public IReadOnlyEntityCommandBuffer BeforeUpdate => _beforeUpdate;
        
        EntityCommandBuffer _afterUpdate;
        public IReadOnlyEntityCommandBuffer AfterUpdate => _afterUpdate;
        
        EntityCommandBuffer _beforeFixedUpdate;
        public IReadOnlyEntityCommandBuffer BeforeFixedUpdate => _beforeFixedUpdate;
        
        EntityCommandBuffer _afterFixedUpdate;
        public IReadOnlyEntityCommandBuffer AfterFixedUpdate => _afterFixedUpdate;
        
        EntityCommandBuffer _beforeLateUpdate;
        public IReadOnlyEntityCommandBuffer BeforeLateUpdate => _beforeLateUpdate;
        
        EntityCommandBuffer _afterLateUpdate;
        public IReadOnlyEntityCommandBuffer AfterLateUpdate => _afterLateUpdate;
        
        EntityCommandBuffer _afterAll;
        public IReadOnlyEntityCommandBuffer AfterAll => _afterAll;
        #endregion

        public void Reset()
        {
            IsEnabled = false;
            _beforeAll.Clear();
            _beforeUpdate.Clear();
            _afterUpdate.Clear();
            _beforeFixedUpdate.Clear();
            _afterFixedUpdate.Clear();
            _beforeLateUpdate.Clear();
            _afterLateUpdate.Clear();
            _afterAll.Clear();
            
            ObjectsStorages.Reset();
            SystemGroups.Reset();
            Entities.Reset();
            Objects.Reset();
        }
        public void Init()
        {
            _beforeAll = new(this);
            _beforeUpdate = new(this);
            _afterUpdate = new(this);
            _beforeFixedUpdate = new(this);
            _afterFixedUpdate = new(this);
            _beforeLateUpdate = new(this);
            _afterLateUpdate = new(this);
            _afterAll = new(this);
            
            ObjectsStorages.Init(this);
            SystemGroups.Init(this);
            InitializeSystems.Init(this);
            Objects.Init(this);
            Entities.Init(this);
            Systems.Init(this);
            
            IsEnabled = true;
        }
        
        public void Update(float deltaTime)
        {
            if (!IsEnabled) return;
            _beforeAll.Execute();
            _beforeUpdate.Execute();
            InitializeSystems.Run();
            Systems.Update(deltaTime);
            Objects.Update(deltaTime);
            _afterUpdate.Execute();
        }
        public void FixedUpdate(float deltaTime)
        {
            if (!IsEnabled) return;
            _beforeFixedUpdate.Execute();
            Systems.FixedUpdate(deltaTime);
            Objects.FixedUpdate(deltaTime);
            _afterFixedUpdate.Execute();
        }
        public void LateUpdate(float deltaTime)
        {
            if (!IsEnabled) return;
            _beforeLateUpdate.Execute();
            Systems.LateUpdate(deltaTime);
            Objects.LateUpdate(deltaTime);
            _afterLateUpdate.Execute();
            _afterAll.Execute();
        }
        
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
        }
    }
}
