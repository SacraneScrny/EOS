using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;

namespace EOS.Systems.CommandBuffer
{
    public interface IReadOnlyEntityCommandBuffer
    {
        public DeferredEntity Create(string name = "");
        public BoundSchedule Schedule(EosEntity entity);
        public BoundSchedule Schedule(DeferredEntity deferred);
        public void Schedule(EosEntity entity, CommandChain chain);
        public void Schedule(DeferredEntity deferred, CommandChain chain);
    }
    public class EntityCommandBuffer : IReadOnlyEntityCommandBuffer
    {
        readonly World _world;
        public EntityCommandBuffer(World world) => _world = world;
        
        readonly List<(string name, DeferredEntity deferred)> _creates = new();
        readonly List<(EosEntity entity, List<Func<EosEntity, bool>> ops)> _batches = new();
        readonly List<(DeferredEntity deferred, List<Func<EosEntity, bool>> ops)> _deferredBatches = new();

        public DeferredEntity Create(string name = "")
        {
            var deferred = new DeferredEntity();
            _creates.Add((name, deferred));
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
            for (int i = 0; i < _creates.Count; i++)
            {
                var (name, deferred) = _creates[i];
                try
                {
                    deferred.Value = new EosEntity(_world, name, true);
                    deferred.IsResolved = true;
                }
                catch { }
            }

            for (int i = 0; i < _batches.Count; i++)
            {
                var (entity, ops) = _batches[i];
                RunOps(entity, ops);
            }

            for (int i = 0; i < _deferredBatches.Count; i++)
            {
                var (deferred, ops) = _deferredBatches[i];
                if (!deferred.IsResolved) continue;
                RunOps(deferred.Value, ops);
            }

            _creates.Clear();
            _batches.Clear();
            _deferredBatches.Clear();
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
                catch { break; }
            }
        }
    }
}