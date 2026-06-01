using System.Collections.Generic;

namespace EOS.Serialization.Snapshot
{
    public class ComponentBag
    {
        public string TypeName { get; set; }
        public string DataTypeName { get; set; }
        public List<ComponentRecord> Items { get; set; } = new();
    }
}
