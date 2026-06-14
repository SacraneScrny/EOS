using System.Collections.Generic;

namespace EOS.Serialization.Snapshot
{
    /// <summary>Serializable snapshot of a single world: its key plus entity, component and context records.</summary>
    public class WorldSnapshot
    {
        /// <summary>The world's key; empty/null identifies the default world on restore.</summary>
        public string WorldKey { get; set; }
        /// <summary>The serializable entities, each with name/active/stable-key/tags/parent; empty by default.</summary>
        public List<EntityRecord> Entities { get; set; } = new();
        /// <summary>Component data grouped one bag per component type; empty by default.</summary>
        public List<ComponentBag> Components { get; set; } = new();
        /// <summary>Captured context values that implement <see cref="ISerializableContext"/>; empty by default.</summary>
        public List<ContextRecord> Context { get; set; } = new();
    }
}
