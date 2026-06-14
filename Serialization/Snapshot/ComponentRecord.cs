namespace EOS.Serialization.Snapshot
{
    /// <summary>One serialized component instance within a <see cref="ComponentBag"/>.</summary>
    public class ComponentRecord
    {
        /// <summary>Snapshot-local id of the owning entity.</summary>
        public int EntityLocalId { get; set; }
        /// <summary>The serialized payload from <c>SerializeData</c>, or null when the component carries no data.</summary>
        public object Data { get; set; }
    }
}
