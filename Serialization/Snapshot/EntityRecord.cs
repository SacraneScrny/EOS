using System.Collections.Generic;

namespace EOS.Serialization.Snapshot
{
    public class EntityRecord
    {
        public int LocalId { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public string StableKey { get; set; }
        public int ParentLocalId { get; set; } = -1;
        public List<TagRecord> Tags { get; set; } = new();
    }
}
