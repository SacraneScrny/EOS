using System.Collections.Generic;

namespace EOS.Serialization.Snapshot
{
    public class WorldSnapshot
    {
        public string WorldKey { get; set; }
        public List<EntityRecord> Entities { get; set; } = new();
        public List<ComponentBag> Components { get; set; } = new();
        public List<ContextRecord> Context { get; set; } = new();
    }
}
