using System.Collections.Generic;

namespace EOS.Serialization.Snapshot
{
    /// <summary>Serializable record for one entity; ids are snapshot-local and remapped to live entities on restore.</summary>
    public class EntityRecord
    {
        /// <summary>The entity's snapshot-local id, used to remap and to reference it as a parent.</summary>
        public int LocalId { get; set; }
        /// <summary>The entity name.</summary>
        public string Name { get; set; }
        /// <summary>The entity's own active flag (<c>IsActiveSelf</c>); effective state is recomputed from restored links.</summary>
        public bool Active { get; set; }
        /// <summary>The serialization-stable key, or null/empty if none.</summary>
        public string StableKey { get; set; }
        /// <summary>Local id of the parent entity, or <c>-1</c> (default) when the entity has no serializable parent.</summary>
        public int ParentLocalId { get; set; } = -1;
        /// <summary>The entity's tags by descriptor; empty by default.</summary>
        public List<TagRecord> Tags { get; set; } = new();
    }
}
