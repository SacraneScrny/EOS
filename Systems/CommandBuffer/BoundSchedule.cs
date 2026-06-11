using System;

using EOS.Entities;
using EOS.Objects;

namespace EOS.Systems.CommandBuffer
{
    public class BoundSchedule
    {
        readonly CommandChain _chain;
        readonly EntityCommandBuffer _ecb;

        internal BoundSchedule(CommandChain chain, EntityCommandBuffer ecb)
        {
            _chain = chain;
            _ecb = ecb;
        }

        public BoundSchedule When<T>() where T : EosObject, new()
            => Wrap(_chain.When<T>());
        public BoundSchedule If(Func<EosEntity, bool> predicate)
            => Wrap(_chain.If(predicate));

        public BoundSchedule Add<T>() where T : EosObject, new()
            => Wrap(_chain.Add<T>());
        public BoundSchedule Add<T>(Action<T> configure) where T : EosObject, new()
            => Wrap(_chain.Add(configure));

        public BoundSchedule Change<T>(Action<T> action) where T : EosObject, new()
            => Wrap(_chain.Change(action));
        public BoundSchedule ChangeOrAdd<T>(Action<T> action) where T : EosObject, new()
            => Wrap(_chain.ChangeOrAdd(action));

        public BoundSchedule Remove<T>() where T : EosObject, new()
            => Wrap(_chain.Remove<T>());
        public BoundSchedule Destroy()
            => Wrap(_chain.Destroy());

        public BoundSchedule SetParent(EosEntity parent)
            => Wrap(_chain.SetParent(parent));
        public BoundSchedule SetParent(DeferredEntity parent)
            => Wrap(_chain.SetParent(parent));
        public BoundSchedule Detach()
            => Wrap(_chain.Detach());

        public BoundSchedule WhenTag(params object[] tags)
            => Wrap(_chain.WhenTag(tags));
        public BoundSchedule WhenNoTag(params object[] tags)
            => Wrap(_chain.WhenNoTag(tags));
        public BoundSchedule WhenAnyTag(params object[] tags)
            => Wrap(_chain.WhenAnyTag(tags));
        public BoundSchedule WhenOneTag(params object[] tags)
            => Wrap(_chain.WhenOneTag(tags));

        public BoundSchedule AddTag(params object[] tags)
            => Wrap(_chain.AddTag(tags));
        public BoundSchedule RemoveTag(params object[] tags)
            => Wrap(_chain.RemoveTag(tags));
        public BoundSchedule SetFlag(object tag, bool on)
            => Wrap(_chain.SetFlag(tag, on));
        public BoundSchedule ClearTags()
            => Wrap(_chain.ClearTags());

        public BoundSchedule Apply(CommandChain chain)
        {
            _chain.Ops.AddRange(chain.Ops);
            return this;
        }

        public BoundSchedule Schedule(EosEntity entity) => _ecb.Schedule(entity);
        public BoundSchedule Schedule(DeferredEntity deferred) => _ecb.Schedule(deferred);

        BoundSchedule Wrap(CommandChain _) => this;
    }
}