using EOS.Entities;

namespace EOS.Systems.CommandBuffer
{
    /// <summary>A handle to an entity created by a command buffer; <see cref="Value"/> resolves when the buffer runs.</summary>
    public class DeferredEntity
    {
        /// <summary>The created entity once resolved; <see cref="EosEntity.Null"/> until then.</summary>
        public EosEntity Value { get; internal set; } = EosEntity.Null;
        /// <summary>True once the buffer has created the entity and <see cref="Value"/> is valid.</summary>
        public bool IsResolved { get; internal set; }
    }
}