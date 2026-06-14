using EOS.Attributes;
using EOS.Core;
using EOS.Objects;

namespace EOS.Systems.Incarnation
{
    /// <summary>Calls <c>Sync</c> on every incarnation once per <c>Update</c>, after all other systems.</summary>
    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncSystem : EosSystem
    {
        /// <summary>Runs in the <c>Update</c> phase.</summary>
        public override UpdateType UpdateType => UpdateType.Update;
        void Execute([Each] IIncarnation inc) => inc.Sync();
    }

    /// <summary>Calls <c>SyncFixed</c> on every incarnation once per <c>FixedUpdate</c>, after all other systems.</summary>
    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncFixedSystem : EosSystem
    {
        /// <summary>Runs in the <c>FixedUpdate</c> phase.</summary>
        public override UpdateType UpdateType => UpdateType.FixedUpdate;
        void Execute([Each] IIncarnation inc) => inc.SyncFixed();
    }

    /// <summary>Calls <c>SyncLate</c> on every incarnation once per <c>LateUpdate</c>, after all other systems.</summary>
    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncLateSystem : EosSystem
    {
        /// <summary>Runs in the <c>LateUpdate</c> phase.</summary>
        public override UpdateType UpdateType => UpdateType.LateUpdate;
        void Execute([Each] IIncarnation inc) => inc.SyncLate();
    }
}
