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
    }
}