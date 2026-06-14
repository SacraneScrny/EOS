using EOS.Entities;

namespace EOS.Storage
{
    /// <summary>Non-generic, index-based view over a <see cref="Storage{T}"/> dense array; the iteration and reactive-watermark surface that query paths build on without knowing the component type.</summary>
    public interface IIndexedStorage
    {
        /// <summary>Number of components in the dense array.</summary>
        int Count { get; }
        /// <summary>The entity owning the component at the given dense index.</summary>
        EosEntity GetOwner(int index);
        /// <summary>The component at the given dense index as a boxed object.</summary>
        object GetAt(int index);
        /// <summary>The component owned by the entity, or null if absent.</summary>
        object TryGetObject(EosEntity entity);
        /// <summary>Whether the component at the given dense index is ready (awoken, started, enabled).</summary>
        bool IsReady(int index);
        /// <summary>Stamps the add-version watermark for the entity's component, signalling the <c>[New]</c> channel, and refreshes readiness.</summary>
        void MarkReady(EosEntity entity);
        /// <summary>Stamps the mark-version watermark for the entity's component, signalling the <c>[Bumped]</c> channel (deduped per frame).</summary>
        void Bump(EosEntity entity);
        /// <summary>Recomputes the ready flag for the entity's component after an enabled/active change.</summary>
        void RefreshReady(EosEntity entity);
        /// <summary>The dense index of the entity's component, or -1 if absent.</summary>
        int IndexOf(EosEntity entity);
        /// <summary>Whether the entity has a ready component in this storage.</summary>
        bool HasReady(EosEntity entity);
        /// <summary>The entity's component if it is ready, otherwise null.</summary>
        object TryGetReadyObject(EosEntity entity);

        /// <summary>Monotonic high-water mark of add-versions, used to early-out <c>[New]</c> reactive scans.</summary>
        ulong MaxAddVersion { get; }
        /// <summary>Monotonic high-water mark of mark-versions, used to early-out <c>[Bumped]</c> reactive scans.</summary>
        ulong MaxMarkVersion { get; }
        /// <summary>The add-version stamped at the given dense index.</summary>
        ulong AddVersionAt(int index);
        /// <summary>The mark-version stamped at the given dense index.</summary>
        ulong MarkVersionAt(int index);
    }
}