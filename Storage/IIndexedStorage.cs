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
        /// <summary>Recomputes the ready flag for the entity's component after an enabled/active change, stamping the <c>[Enabled]</c>/<c>[Disabled]</c> channel on a transition; <paramref name="cascade"/> marks the edge as driven by an entity/parent active change rather than an explicit <c>SetEnabled</c>.</summary>
        void RefreshReady(EosEntity entity, bool cascade);
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
        /// <summary>Monotonic high-water mark of enable-versions, used to early-out <c>[Enabled]</c> reactive scans.</summary>
        ulong MaxEnableVersion { get; }
        /// <summary>Monotonic high-water mark of disable-versions, used to early-out <c>[Disabled]</c> reactive scans.</summary>
        ulong MaxDisableVersion { get; }
        /// <summary>The add-version stamped at the given dense index.</summary>
        ulong AddVersionAt(int index);
        /// <summary>The mark-version stamped at the given dense index.</summary>
        ulong MarkVersionAt(int index);
        /// <summary>The enable-version stamped at the given dense index (last disabled→enabled transition).</summary>
        ulong EnableVersionAt(int index);
        /// <summary>The disable-version stamped at the given dense index (last enabled→disabled transition).</summary>
        ulong DisableVersionAt(int index);
        /// <summary>Whether the last enable transition at the given dense index was a cascade (entity/parent re-activation).</summary>
        bool EnableCascadeAt(int index);
        /// <summary>Whether the last disable transition at the given dense index was a cascade (entity/parent deactivation).</summary>
        bool DisableCascadeAt(int index);

        /// <summary>Monotonic high-water mark of remove-versions, used to early-out <c>[Removed]</c> reactive scans.</summary>
        ulong MaxRemoveVersion { get; }
        /// <summary>Number of live entries in the removal log.</summary>
        int RemovedCount { get; }
        /// <summary>The entity whose component was removed at the given removal-log index (may be a stale handle for destroy cascades).</summary>
        EosEntity RemovedOwnerAt(int index);
        /// <summary>The remove-version stamped at the given removal-log index.</summary>
        ulong RemovedVersionAt(int index);
        /// <summary>Whether the removal at the given removal-log index was a cascade (entity destroy) rather than an explicit <c>Remove&lt;T&gt;()</c>.</summary>
        bool RemovedCascadeAt(int index);
    }
}