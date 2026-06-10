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
                    Discard(obj);
                    _batch.RemoveAt(i);
                    World.Objects.MarkFailed(obj);
                    continue;
                }
                if (!World.Entities.IsActive(obj.Entity)) continue;

                obj.Awake();
                if (obj.IsFailed)
                {
                    EosLog.Warning($"{obj.GetType().Name} failed to awake, disposing", nameof(InitializeSystemRunner));
                    Discard(obj);
                    _batch.RemoveAt(i);
                    World.Objects.MarkFailed(obj);
                }
            }

            for (int i = _batch.Count - 1; i >= 0; i--)
            {
                var obj = _batch[i];
                if (!obj.IsAwaken || obj.IsDisposed) continue;

                obj.Start();
                if (obj.IsFailed)
                {
                    EosLog.Warning($"{obj.GetType().Name} failed to start, disposing", nameof(InitializeSystemRunner));
                    Discard(obj);
                    World.Objects.MarkFailed(obj);
                    continue;
                }

                World.ObjectsStorages.MarkReady(obj);
                World.Objects.MarkInitialized(obj);
            }
        }

        void Discard(EosObject obj)
        {
            if (!World.ObjectsStorages.RemoveFromStorage(obj))
                obj.Dispose();
        }
    }
}
