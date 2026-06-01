using System;
using EOS.Serialization.Snapshot;

namespace EOS.Serialization
{
    public static class WorldLoader
    {
        public static Func<UniverseSnapshot> OnLoad;
        public static Action<UniverseSnapshot> OnSave;

        public static void Reset()
        {
            OnLoad = null;
            OnSave = null;
        }
    }
}
