using System.Collections.Generic;

namespace EOS.Serialization.Snapshot
{
    /// <summary>Top-level serializable snapshot of all captured worlds; the plain-object payload passed through the <see cref="WorldLoader"/> hooks.</summary>
    public class UniverseSnapshot
    {
        /// <summary>The captured worlds, one record per serializable world; empty by default.</summary>
        public List<WorldSnapshot> Worlds { get; set; } = new();
    }
}
