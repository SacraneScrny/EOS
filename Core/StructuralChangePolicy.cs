namespace EOS.Core
{
    /// <summary>
    /// Defines how the world reacts to a direct structural change
    /// (creating/destroying an entity, adding/removing a component)
    /// attempted while systems are iterating.
    /// </summary>
    public enum StructuralChangePolicy
    {
        /// <summary>Throw an <see cref="System.InvalidOperationException"/>. Default.</summary>
        Throw,
        /// <summary>Log an error but still apply the change (legacy behaviour).</summary>
        Warn,
        /// <summary>Do nothing special and apply the change immediately.</summary>
        Allow
    }
}
