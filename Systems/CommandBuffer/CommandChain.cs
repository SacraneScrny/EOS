using System;
using System.Collections.Generic;

using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;

namespace EOS.Systems.CommandBuffer
{
    /// <summary>A reusable, fluent list of conditional ops applied to an entity when a command buffer runs; a false condition short-circuits the rest of the chain.</summary>
    public class CommandChain
    {
        internal readonly List<Func<EosEntity, bool>> Ops = new();

        /// <summary>Continues only if the entity has component <typeparamref name="T"/>.</summary>
        public CommandChain When<T>() where T : EosObject, new()
        {
            Ops.Add(e => e.Has<T>());
            return this;
        }
        /// <summary>Continues only if the predicate returns true for the entity.</summary>
        public CommandChain If(Func<EosEntity, bool> predicate)
        {
            Ops.Add(predicate);
            return this;
        }

        /// <summary>Adds component <typeparamref name="T"/> if missing.</summary>
        public CommandChain Add<T>() where T : EosObject, new()
        {
            Ops.Add(e => { if (!e.Has<T>()) e.Add<T>(); return true; });
            return this;
        }
        /// <summary>Adds component <typeparamref name="T"/> if missing, running <paramref name="configure"/> only on the fresh add.</summary>
        public CommandChain Add<T>(Action<T> configure) where T : EosObject, new()
        {
            Ops.Add(e => { if (!e.Has<T>()) configure(e.Add<T>()); return true; });
            return this;
        }

        /// <summary>Mutates component <typeparamref name="T"/> only if present.</summary>
        public CommandChain Change<T>(Action<T> action) where T : EosObject, new()
        {
            Ops.Add(e => { if (e.Has<T>()) action(e.Get<T>()); return true; });
            return this;
        }
        /// <summary>Mutates component <typeparamref name="T"/>, adding it first if absent.</summary>
        public CommandChain ChangeOrAdd<T>(Action<T> action) where T : EosObject, new()
        {
            Ops.Add(e => { action(e.Has<T>() ? e.Get<T>() : e.Add<T>()); return true; });
            return this;
        }

        /// <summary>Removes component <typeparamref name="T"/>.</summary>
        public CommandChain Remove<T>() where T : EosObject, new()
        {
            Ops.Add(e => { e.Remove<T>(); return true; });
            return this;
        }
        /// <summary>Destroys the entity (and its subtree).</summary>
        public CommandChain Destroy()
        {
            Ops.Add(e => { e.Destroy(); return true; });
            return this;
        }

        /// <summary>Reparents the entity under <paramref name="parent"/>.</summary>
        public CommandChain SetParent(EosEntity parent)
        {
            Ops.Add(e => { e.SetParent(parent); return true; });
            return this;
        }
        /// <summary>Reparents the entity under a deferred entity once it has resolved.</summary>
        public CommandChain SetParent(DeferredEntity parent)
        {
            Ops.Add(e => { if (parent.IsResolved) e.SetParent(parent.Value); return true; });
            return this;
        }
        /// <summary>Detaches the entity from its parent.</summary>
        public CommandChain Detach()
        {
            Ops.Add(e => { e.Detach(); return true; });
            return this;
        }

        /// <summary>Continues only if the entity has all the given tags.</summary>
        public CommandChain WhenTag(params object[] tags)
        {
            Ops.Add(e => e.HasAllTags(tags));
            return this;
        }
        /// <summary>Continues only if the entity has none of the given tags.</summary>
        public CommandChain WhenNoTag(params object[] tags)
        {
            Ops.Add(e => !e.HasAnyTag(tags));
            return this;
        }
        /// <summary>Continues only if the entity has any of the given tags.</summary>
        public CommandChain WhenAnyTag(params object[] tags)
        {
            Ops.Add(e => e.HasAnyTag(tags));
            return this;
        }
        /// <summary>Continues only if the entity has exactly one of the given tags.</summary>
        public CommandChain WhenOneTag(params object[] tags)
        {
            Ops.Add(e => e.HasOneTag(tags));
            return this;
        }

        /// <summary>Adds the given tags to the entity.</summary>
        public CommandChain AddTag(params object[] tags)
        {
            Ops.Add(e => { e.AddTag(tags); return true; });
            return this;
        }
        /// <summary>Removes the given tags from the entity.</summary>
        public CommandChain RemoveTag(params object[] tags)
        {
            Ops.Add(e => { e.RemoveTag(tags); return true; });
            return this;
        }
        /// <summary>Sets or clears a single tag flag on the entity.</summary>
        public CommandChain SetFlag(object tag, bool on)
        {
            Ops.Add(e => { e.SetFlag(tag, on); return true; });
            return this;
        }
        /// <summary>Clears all tags on the entity.</summary>
        public CommandChain ClearTags()
        {
            Ops.Add(e => { e.ClearTags(); return true; });
            return this;
        }
    }
}