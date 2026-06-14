using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Logging;

namespace EOS.Systems.CommandBuffer
{
    /// <summary>Scheduling surface of an <see cref="EntityCommandBuffer"/>: defer entity creation and per-entity operation chains for later execution.</summary>
    public interface IEntityCommandScheduler
    {
        /// <summary>Schedules creation of a new (active) entity and returns a <see cref="DeferredEntity"/> handle resolved when the buffer runs.</summary>
        public DeferredEntity Create(string name = "", bool isSerializable = true);
        /// <summary>Begins a fluent operation chain bound to an existing entity.</summary>
        public BoundSchedule Schedule(EosEntity entity);
        /// <summary>Begins a fluent operation chain bound to a deferred (not-yet-created) entity.</summary>
        public BoundSchedule Schedule(DeferredEntity deferred);
        /// <summary>Queues a copy of an existing <see cref="CommandChain"/> against an entity for reuse across entities.</summary>
        public void Schedule(EosEntity entity, CommandChain chain);
        /// <summary>Queues a copy of an existing <see cref="CommandChain"/> against a deferred entity.</summary>
        public void Schedule(DeferredEntity deferred, CommandChain chain);
    }
    /// <summary>Buffers deferred structural changes (creates and per-entity operation chains) and applies them all at its scheduled point in the frame loop.</summary>
    public class EntityCommandBuffer : IEntityCommandScheduler
    {
        readonly World _world;
        /// <summary>Creates a command buffer bound to the given world.</summary>
        public EntityCommandBuffer(World world) => _world = world;

        readonly List<(string name, bool isSerializable, DeferredEntity deferred)> _creates = new();
        readonly List<(EosEntity entity, List<Func<EosEntity, bool>> ops)> _batches = new();
        readonly List<(DeferredEntity deferred, List<Func<EosEntity, bool>> ops)> _deferredBatches = new();

        /// <summary>Schedules creation of a new active entity and returns a <see cref="DeferredEntity"/> handle resolved when the buffer executes.</summary>
        public DeferredEntity Create(string name = "", bool isSerializable = true)
        {
            var deferred = new DeferredEntity();
            _creates.Add((name, isSerializable, deferred));
            return deferred;
        }

        /// <summary>Begins a fluent operation chain bound to an existing entity, queued for execution.</summary>
        public BoundSchedule Schedule(EosEntity entity)
        {
            var chain = new CommandChain();
            _batches.Add((entity, chain.Ops));
            return new BoundSchedule(chain, this);
        }
        /// <summary>Begins a fluent operation chain bound to a deferred entity, queued for execution.</summary>
        public BoundSchedule Schedule(DeferredEntity deferred)
        {
            var chain = new CommandChain();
            _deferredBatches.Add((deferred, chain.Ops));
            return new BoundSchedule(chain, this);
        }
        /// <summary>Queues a copy of an existing chain's operations against an entity, so one chain can be reused across entities.</summary>
        public void Schedule(EosEntity entity, CommandChain chain)
            => _batches.Add((entity, new List<Func<EosEntity, bool>>(chain.Ops)));
        /// <summary>Queues a copy of an existing chain's operations against a deferred entity.</summary>
        public void Schedule(DeferredEntity deferred, CommandChain chain)
            => _deferredBatches.Add((deferred, new List<Func<EosEntity, bool>>(chain.Ops)));

        /// <summary>Applies all buffered creates and operation chains (draining ops scheduled during the pass, guarded against runaway), then clears the buffer.</summary>
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

        /// <summary>Discards all buffered creates and operation chains without executing them.</summary>
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