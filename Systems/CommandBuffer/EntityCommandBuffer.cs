using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Logging;

namespace EOS.Systems.CommandBuffer
{
    public interface IReadOnlyEntityCommandBuffer
    {
        public DeferredEntity Create(string name = "", bool isSerializable = true);
        public BoundSchedule Schedule(EosEntity entity);
        public BoundSchedule Schedule(DeferredEntity deferred);
        public void Schedule(EosEntity entity, CommandChain chain);
        public void Schedule(DeferredEntity deferred, CommandChain chain);
    }
    public class EntityCommandBuffer : IReadOnlyEntityCommandBuffer
    {
        readonly World _world;
        public EntityCommandBuffer(World world) => _world = world;

        readonly List<(string name, bool isSerializable, DeferredEntity deferred)> _creates = new();
        readonly List<(EosEntity entity, List<Func<EosEntity, bool>> ops)> _batches = new();
        readonly List<(DeferredEntity deferred, List<Func<EosEntity, bool>> ops)> _deferredBatches = new();

        public DeferredEntity Create(string name = "", bool isSerializable = true)
        {
            var deferred = new DeferredEntity();
            _creates.Add((name, isSerializable, deferred));
            return deferred;
        }

        public BoundSchedule Schedule(EosEntity entity)
        {
            var chain = new CommandChain();
            _batches.Add((entity, chain.Ops));
            return new BoundSchedule(chain, this);
        }
        public BoundSchedule Schedule(DeferredEntity deferred)
        {
            var chain = new CommandChain();
            _deferredBatches.Add((deferred, chain.Ops));
            return new BoundSchedule(chain, this);
        }
        public void Schedule(EosEntity entity, CommandChain chain)
            => _batches.Add((entity, chain.Ops));
        public void Schedule(DeferredEntity deferred, CommandChain chain)
            => _deferredBatches.Add((deferred, chain.Ops));

        public void Execute()
        {
            int createIndex = 0;
            int batchIndex = 0;
            int deferredBatchIndex = 0;
            
            int totalOperationsProcessed = 0;
            const int MAX_DRAIN_OPERATIONS = 10000; 

            while (createIndex < _creates.Count || 
                   batchIndex < _batches.Count || 
                   deferredBatchIndex < _deferredBatches.Count)
            {
                totalOperationsProcessed++;
                if (totalOperationsProcessed > MAX_DRAIN_OPERATIONS)
                {
                    EosLog.Error($"EntityCommandBuffer drain exceeded maximum operations ({MAX_DRAIN_OPERATIONS}). Possible infinite loop detected. Clearing buffer.", nameof(EntityCommandBuffer));
                    Clear();
                    return;
                }

                while (createIndex < _creates.Count)
                {
                    var (name, isSerializable, deferred) = _creates[createIndex];
                    try
                    {
                        deferred.Value = new EosEntity(_world, name, true, isSerializable);
                        deferred.IsResolved = true;
                    }
                    catch (Exception ex)
                    {
                        EosLog.Error($"Failed to create entity '{name}': {ex.Message}", nameof(EntityCommandBuffer));
                    }
                    createIndex++;
                }

                while (batchIndex < _batches.Count)
                {
                    var (entity, ops) = _batches[batchIndex];
                    RunOps(entity, ops);
                    batchIndex++;
                }

                while (deferredBatchIndex < _deferredBatches.Count)
                {
                    var (deferred, ops) = _deferredBatches[deferredBatchIndex];
                    if (deferred.IsResolved)
                    {
                        RunOps(deferred.Value, ops);
                    }
                    else
                    {
                        EosLog.Warning($"Deferred entity in batch is not resolved. Skipping operations.", nameof(EntityCommandBuffer));
                    }
                    deferredBatchIndex++;
                }
            }

            Clear();
        }

        public void Clear()
        {
            _creates.Clear();
            _batches.Clear();
            _deferredBatches.Clear();
        }

        static void RunOps(EosEntity entity, List<Func<EosEntity, bool>> ops)
        {
            if (!entity.IsValid) return;
            for (int j = 0; j < ops.Count; j++)
            {
                try { if (!ops[j](entity)) break; }
                catch (Exception ex)
                {
                    EosLog.Error($"Op failed on entity {entity}: {ex.Message}", nameof(EntityCommandBuffer));
                    break;
                }
            }
        }
    }
}