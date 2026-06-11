using EOS.Entities;

namespace EOS.Hierarchy
{
    public readonly struct ParentChanged
    {
        public readonly EosEntity Child;
        public readonly EosEntity OldParent;
        public readonly EosEntity NewParent;

        public ParentChanged(EosEntity child, EosEntity oldParent, EosEntity newParent)
        {
            Child = child;
            OldParent = oldParent;
            NewParent = newParent;
        }
    }
}
