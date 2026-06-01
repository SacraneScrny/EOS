using EOS.Attributes;
using EOS.Core;
using EOS.Objects;

namespace EOS.Systems.Incarnation
{
    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.Update;
        void Execute([Each] IIncarnation inc) => inc.Sync();
    }

    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncFixedSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.FixedUpdate;
        void Execute([Each] IIncarnation inc) => inc.SyncFixed();
    }

    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncLateSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.LateUpdate;
        void Execute([Each] IIncarnation inc) => inc.SyncLate();
    }
}
