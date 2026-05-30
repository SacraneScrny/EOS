using System;
using System.Collections.Generic;

using EOS.Entities;

namespace EOS.Systems.CommandBuffer
{
    public class EntityCommandBuffer
    {
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
            // проход 1 — создаём сущности и резолвим токены
            for (int i = 0; i < _creates.Count; i++)
            {
                var (name, deferred) = _creates[i];
                try
                {
                    deferred.Value = new EosEntity(name);
                    deferred.IsResolved = true;
                }
                catch { }
            }

            // проход 2 — обычные батчи
            for (int i = 0; i < _batches.Count; i++)
            {
                var (entity, ops) = _batches[i];
                RunOps(entity, ops);
            }

            // проход 3 — деферред батчи
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