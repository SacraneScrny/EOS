using System;
using EOS.Serialization.Snapshot;

namespace EOS.Serialization
{
    /// <summary>Static persistence seam: the consumer sets these hooks to supply/persist snapshots. <c>Universe.Boot()</c> invokes <see cref="OnLoad"/> automatically.</summary>
    public static class WorldLoader
    {
        /// <summary>Consumer-supplied loader invoked on boot; return a snapshot to restore, or null for a fresh start.</summary>
        public static Func<UniverseSnapshot> OnLoad;
        /// <summary>Consumer-supplied saver invoked by <see cref="WorldSerializer.Save"/> with the captured snapshot.</summary>
        public static Action<UniverseSnapshot> OnSave;

        /// <summary>Clears both hooks; called on domain reset so handlers don't leak across sessions.</summary>
        public static void Reset()
        {
            OnLoad = null;
            OnSave = null;
        }
    }
}
