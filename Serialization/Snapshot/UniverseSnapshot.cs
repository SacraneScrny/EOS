using System.Collections.Generic;

namespace EOS.Serialization.Snapshot
{
    public class UniverseSnapshot
    {
        public List<WorldSnapshot> Worlds { get; set; } = new();
    }
}
