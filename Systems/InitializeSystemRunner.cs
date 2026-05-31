using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
using EOS.Logging;
using EOS.Objects;

namespace EOS.Systems
{
    public class InitializeSystemRunner : WorldBound
    {
        readonly List<EosObject> _batch = new();
        public IReadOnlyList<EosObject> Batch => _batch;

        internal void Run()
        {
            _batch.Clear();
            _batch.AddRange(World.Objects.Waiting);

            for (int i = _batch.Count - 1; i >= 0; i--)
            {
                var obj = _batch[i];

                if (!World.Entities.IsValid(obj.Entity))
                {
                    EosLog.Warning($"{obj.GetType().Name} has invalid entity, disposing", nameof(InitializeSystemRunner));
                    obj.Dispose();
                    _batch.RemoveAt(i);
                    World.Objects.MarkFailed(obj);
                    continue;
                }
                if (!World.Entities.IsActive(obj.Entity)) continue;

                try { obj.Awake(); }
                catch (Exception ex) { EosLog.Error($"{obj.GetType().Name}.Awake threw: {ex.Message}", nameof(InitializeSystemRunner)); }
            }

            for (int i = _batch.Count - 1; i >= 0; i--)
            {
                var obj = _batch[i];
                if (!obj.IsAwaken) continue;
                try { obj.Start(); }
                catch (Exception ex) { EosLog.Error($"{obj.GetType().Name}.Start threw: {ex.Message}", nameof(InitializeSystemRunner)); }
                World.ObjectsStorages.MarkReady(obj);
                World.Objects.MarkInitialized(obj);
            }
        }
    }
}
