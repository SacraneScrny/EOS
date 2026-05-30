using EOS.Entities;

namespace EOS.Systems.CommandBuffer
{
    public class DeferredEntity
    {
        public EosEntity Value { get; internal set; } = EosEntity.Null;
        public bool IsResolved { get; internal set; }
    }
}