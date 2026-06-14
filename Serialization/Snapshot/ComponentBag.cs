using System.Collections.Generic;

namespace EOS.Serialization.Snapshot
{
    /// <summary>All serialized instances of one component type, grouped for restore.</summary>
    public class ComponentBag
    {
        /// <summary>Assembly-qualified name of the component type; resolved version-tolerantly on restore.</summary>
        public string TypeName { get; set; }
        /// <summary>Assembly-qualified name of the component's <c>DataType</c> payload, or null when the component is not serializable.</summary>
        public string DataTypeName { get; set; }
        /// <summary>One record per component instance (owning entity plus optional data); empty by default.</summary>
        public List<ComponentRecord> Items { get; set; } = new();
    }
}
