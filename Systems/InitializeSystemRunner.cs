using System.Collections.Generic;

using EOS.Entities;
using EOS.Objects;

namespace EOS.Systems
{
    internal static class InitializeSystemRunner
    {
        static readonly List<EosObject> _batch = new();

        public static void Run()
        {
            _batch.Clear();
            _batch.AddRange(ObjectsContainer.Waiting);

            for (int i = _batch.Count - 1; i >= 0; i--)
            {
                var obj = _batch[i];

                if (!EntitiesContainer.IsValid(obj.Entity))
                {
                    obj.Dispose();
                    _batch.RemoveAt(i);
                    ObjectsContainer.MarkFailed(obj);
                    continue;
                }
                if (!EntitiesContainer.IsActive(obj.Entity)) continue;

                obj.Awake();
            }

            for (int i = _batch.Count - 1; i >= 0; i--)
            {
                var obj = _batch[i];
                obj.Start();
                ObjectsContainer.MarkInitialized(obj);
            }
        }
    }
}
