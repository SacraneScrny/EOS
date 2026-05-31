using System.Collections.Generic;

using EOS.Core;
using EOS.Entities;
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
                    obj.Dispose();
                    _batch.RemoveAt(i);
                    World.Objects.MarkFailed(obj);
                    continue;
                }
                if (!World.Entities.IsActive(obj.Entity)) continue;

                obj.Awake();
            }

            for (int i = _batch.Count - 1; i >= 0; i--)
            {
                var obj = _batch[i];
                obj.Start();
                World.ObjectsStorages.MarkReady(obj);
                World.Objects.MarkInitialized(obj);
            }
        }
    }
}
