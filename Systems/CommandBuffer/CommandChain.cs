using System;
using System.Collections.Generic;

using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;

namespace EOS.Systems.CommandBuffer
{
    public class CommandChain
    {
        internal readonly List<Func<EosEntity, bool>> Ops = new();

        public CommandChain When<T>() where T : EosObject, new()
        {
            Ops.Add(e => e.Has<T>());
            return this;
        }
        public CommandChain If(Func<EosEntity, bool> predicate)
        {
            Ops.Add(predicate);
            return this;
        }

        public CommandChain Add<T>() where T : EosObject, new()
        {
            Ops.Add(e => { if (!e.Has<T>()) e.Add<T>(); return true; });
            return this;
        }
        public CommandChain Add<T>(Action<T> configure) where T : EosObject, new()
        {
            Ops.Add(e => { if (!e.Has<T>()) configure(e.Add<T>()); return true; });
            return this;
        }

        public CommandChain Change<T>(Action<T> action) where T : EosObject, new()
        {
            Ops.Add(e => { if (e.Has<T>()) action(e.Get<T>()); return true; });
            return this;
        }
        public CommandChain ChangeOrAdd<T>(Action<T> action) where T : EosObject, new()
        {
            Ops.Add(e => { action(e.Has<T>() ? e.Get<T>() : e.Add<T>()); return true; });
            return this;
        }

        public CommandChain Remove<T>() where T : EosObject, new()
        {
            Ops.Add(e => { e.Remove<T>(); return true; });
            return this;
        }
        public CommandChain Destroy()
        {
            Ops.Add(e => { e.Destroy(); return true; });
            return this;
        }

        public CommandChain SetParent(EosEntity parent)
        {
            Ops.Add(e => { e.SetParent(parent); return true; });
            return this;
        }
        public CommandChain SetParent(DeferredEntity parent)
        {
            Ops.Add(e => { if (parent.IsResolved) e.SetParent(parent.Value); return true; });
            return this;
        }
        public CommandChain Detach()
        {
            Ops.Add(e => { e.Detach(); return true; });
            return this;
        }

        public CommandChain WhenTag(params object[] tags)
        {
            Ops.Add(e => e.HasAllTags(tags));
            return this;
        }
        public CommandChain WhenNoTag(params object[] tags)
        {
            Ops.Add(e => !e.HasAnyTag(tags));
            return this;
        }
        public CommandChain WhenAnyTag(params object[] tags)
        {
            Ops.Add(e => e.HasAnyTag(tags));
            return this;
        }
        public CommandChain WhenOneTag(params object[] tags)
        {
            Ops.Add(e => e.HasOneTag(tags));
            return this;
        }

        public CommandChain AddTag(params object[] tags)
        {
            Ops.Add(e => { e.AddTag(tags); return true; });
            return this;
        }
        public CommandChain RemoveTag(params object[] tags)
        {
            Ops.Add(e => { e.RemoveTag(tags); return true; });
            return this;
        }
        public CommandChain SetFlag(object tag, bool on)
        {
            Ops.Add(e => { e.SetFlag(tag, on); return true; });
            return this;
        }
        public CommandChain ClearTags()
        {
            Ops.Add(e => { e.ClearTags(); return true; });
            return this;
        }
    }
}