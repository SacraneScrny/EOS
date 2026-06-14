namespace EOS.Core
{
    /// <summary>Controls how the world reacts to direct structural changes (create/add/remove/destroy) attempted during system iteration; defer them through an <see cref="EOS.Systems.CommandBuffer.EntityCommandBuffer"/> instead.</summary>
    public enum StructuralChangePolicy
    {
        /// <summary>Throw an <see cref="System.InvalidOperationException"/> on a structural change during iteration (the default, safest mode).</summary>
        Throw,
        /// <summary>Log a warning but allow the structural change during iteration.</summary>
        Warn,
        /// <summary>Silently allow structural changes during iteration.</summary>
        Allow
    }
}
