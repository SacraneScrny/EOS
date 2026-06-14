namespace EOS.Serialization.Snapshot
{
    /// <summary>One serialized tag by descriptor: either a string tag (<see cref="Name"/>) or an enum tag (<see cref="EnumType"/> + <see cref="EnumValue"/>).</summary>
    public class TagRecord
    {
        /// <summary>The string tag name, or null when this record is an enum tag.</summary>
        public string Name { get; set; }
        /// <summary>Assembly-qualified name of the enum type, or null when this record is a string tag.</summary>
        public string EnumType { get; set; }
        /// <summary>The enum tag's numeric value (meaningful only when <see cref="EnumType"/> is set).</summary>
        public long EnumValue { get; set; }
    }
}
