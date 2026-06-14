using System;

using EOS.Entities;
using EOS.Objects;

namespace EOS.Systems.CommandBuffer
{
    /// <summary>Fluent builder returned by <see cref="EntityCommandBuffer.Schedule(EosEntity)"/> that appends conditions and operations to one entity's deferred chain.</summary>
    public class BoundSchedule
    {
        readonly CommandChain _chain;
        readonly EntityCommandBuffer _ecb;

        internal BoundSchedule(CommandChain chain, EntityCommandBuffer ecb)
        {
            _chain = chain;
            _ecb = ecb;
        }

        /// <summary>Condition: continues the chain only if the entity has component <typeparamref name="T"/>, else short-circuits the rest.</summary>
        public BoundSchedule When<T>() where T : EosObject, new()
            => Wrap(_chain.When<T>());
        /// <summary>Condition: continues the chain only if the predicate holds for the entity, else short-circuits the rest.</summary>
        public BoundSchedule If(Func<EosEntity, bool> predicate)
            => Wrap(_chain.If(predicate));

        /// <summary>Adds component <typeparamref name="T"/> if the entity does not already have it.</summary>
        public BoundSchedule Add<T>() where T : EosObject, new()
            => Wrap(_chain.Add<T>());
        /// <summary>Adds component <typeparamref name="T"/> if missing, running <paramref name="configure"/> only on a fresh add.</summary>
        public BoundSchedule Add<T>(Action<T> configure) where T : EosObject, new()
            => Wrap(_chain.Add(configure));

        /// <summary>Mutates component <typeparamref name="T"/> via <paramref name="action"/> only if the entity already has it.</summary>
        public BoundSchedule Change<T>(Action<T> action) where T : EosObject, new()
            => Wrap(_chain.Change(action));
        /// <summary>Adds component <typeparamref name="T"/> if missing, then always runs <paramref name="action"/> on it.</summary>
        public BoundSchedule ChangeOrAdd<T>(Action<T> action) where T : EosObject, new()
            => Wrap(_chain.ChangeOrAdd(action));

        /// <summary>Removes component <typeparamref name="T"/> from the entity.</summary>
        public BoundSchedule Remove<T>() where T : EosObject, new()
            => Wrap(_chain.Remove<T>());
        /// <summary>Destroys the entity (and its hierarchy subtree).</summary>
        public BoundSchedule Destroy()
            => Wrap(_chain.Destroy());

        /// <summary>Reparents the entity under the given parent.</summary>
        public BoundSchedule SetParent(EosEntity parent)
            => Wrap(_chain.SetParent(parent));
        /// <summary>Reparents the entity under a deferred (not-yet-created) parent.</summary>
        public BoundSchedule SetParent(DeferredEntity parent)
            => Wrap(_chain.SetParent(parent));
        /// <summary>Detaches the entity from its current parent.</summary>
        public BoundSchedule Detach()
            => Wrap(_chain.Detach());

        /// <summary>Condition: continues only if the entity has all of the given tags.</summary>
        public BoundSchedule WhenTag(params object[] tags)
            => Wrap(_chain.WhenTag(tags));
        /// <summary>Condition: continues only if the entity has none of the given tags.</summary>
        public BoundSchedule WhenNoTag(params object[] tags)
            => Wrap(_chain.WhenNoTag(tags));
        /// <summary>Condition: continues only if the entity has at least one of the given tags.</summary>
        public BoundSchedule WhenAnyTag(params object[] tags)
            => Wrap(_chain.WhenAnyTag(tags));
        /// <summary>Condition: continues only if the entity has exactly one of the given tags.</summary>
        public BoundSchedule WhenOneTag(params object[] tags)
            => Wrap(_chain.WhenOneTag(tags));

        /// <summary>Adds the given tags to the entity.</summary>
        public BoundSchedule AddTag(params object[] tags)
            => Wrap(_chain.AddTag(tags));
        /// <summary>Removes the given tags from the entity.</summary>
        public BoundSchedule RemoveTag(params object[] tags)
            => Wrap(_chain.RemoveTag(tags));
        /// <summary>Sets or clears a single tag/flag on the entity.</summary>
        public BoundSchedule SetFlag(object tag, bool on)
            => Wrap(_chain.SetFlag(tag, on));
        /// <summary>Clears all tags from the entity.</summary>
        public BoundSchedule ClearTags()
            => Wrap(_chain.ClearTags());

        /// <summary>Appends the operations of another <see cref="CommandChain"/> to this one.</summary>
        public BoundSchedule Apply(CommandChain chain)
        {
            _chain.Ops.AddRange(chain.Ops);
            return this;
        }

        /// <summary>Ends this chain and begins a new one bound to another existing entity in the same buffer.</summary>
        public BoundSchedule Schedule(EosEntity entity) => _ecb.Schedule(entity);
        /// <summary>Ends this chain and begins a new one bound to another deferred entity in the same buffer.</summary>
        public BoundSchedule Schedule(DeferredEntity deferred) => _ecb.Schedule(deferred);

        BoundSchedule Wrap(CommandChain _) => this;
    }
}