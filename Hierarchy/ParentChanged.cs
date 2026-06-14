using EOS.Entities;

namespace EOS.Hierarchy
{
    /// <summary>Struct event emitted on every hierarchy link change; read it via an <c>EventExecute(ParentChanged)</c> on a system.</summary>
    public readonly struct ParentChanged
    {
        /// <summary>The entity whose parent changed.</summary>
        public readonly EosEntity Child;
        /// <summary>The parent before the change (<see cref="EosEntity.Null"/> if it was a root).</summary>
        public readonly EosEntity OldParent;
        /// <summary>The parent after the change (<see cref="EosEntity.Null"/> on detach).</summary>
        public readonly EosEntity NewParent;

        /// <summary>Creates the event capturing the child and its old/new parents.</summary>
        public ParentChanged(EosEntity child, EosEntity oldParent, EosEntity newParent)
        {
            Child = child;
            OldParent = oldParent;
            NewParent = newParent;
        }
    }
}
