using System;
using System.Collections.Generic;

using EOS.Entities;

namespace EOS.Systems.CommandBuffer
{
    public class EntityCommandBuffer
    {
        readonly List<(EosEntity entity, List<Func<EosEntity, bool>> ops)> _batches = new();

        // режим 1 — инлайн билдер
        public BoundSchedule Schedule(EosEntity entity)
        {
            var chain = new CommandChain();
            _batches.Add((entity, chain.Ops));
            return new BoundSchedule(chain, this);
        }

        // режим 2 — закинуть кешированный чейн
        public void Schedule(EosEntity entity, CommandChain chain)
            => _batches.Add((entity, chain.Ops));

        public void Execute()
        {
            for (int i = 0; i < _batches.Count; i++)
            {
                var (entity, ops) = _batches[i];
                if (!entity.IsValid) continue;

                for (int j = 0; j < ops.Count; j++)
                {
                    try { if (!ops[j](entity)) break; }
                    catch { break; }
                }
            }
            _batches.Clear();
        }

        public void Clear() => _batches.Clear();
    }
}