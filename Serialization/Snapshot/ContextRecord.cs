namespace EOS.Serialization.Snapshot
{
    /// <summary>One captured world-context value.</summary>
    public class ContextRecord
    {
        /// <summary>Assembly-qualified name of the value's type; resolved version-tolerantly on restore.</summary>
        public string TypeName { get; set; }
        /// <summary>The captured context value.</summary>
        public object Value { get; set; }
    }
}
