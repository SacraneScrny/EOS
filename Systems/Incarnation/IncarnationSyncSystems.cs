using EOS.Attributes;
using EOS.Core;
using EOS.Loader;
using EOS.Systems.Groups;

using IncarnationObj = EOS.Objects.Incarnation;

namespace EOS.Systems.Incarnation
{
    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.Update;
        void Execute(IncarnationObj inc) => IncarnationBridge.Binder?.Sync(inc.Entity, inc.View);
    }

    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncFixedSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.FixedUpdate;
        void Execute(IncarnationObj inc) => IncarnationBridge.Binder?.SyncFixed(inc.Entity, inc.View);
    }

    [Group(typeof(IncarnationGroup))]
    [UpdateOrder(UpdateOrderPhase.AfterAll)]
    public class IncarnationSyncLateSystem : EosSystem
    {
        public override UpdateType UpdateType => UpdateType.LateUpdate;
        void Execute(IncarnationObj inc) => IncarnationBridge.Binder?.SyncLate(inc.Entity, inc.View);
    }
}
