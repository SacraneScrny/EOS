namespace EOS.Core
{
    /// <summary>Identifies one of a world's three per-frame phases; routes a system to a phase (via <c>EosSystem.UpdateType</c>) and tags <see cref="World.CurrentPhase"/>.</summary>
    public enum UpdateType
    {
        /// <summary>The main update phase; the only phase that runs <c>BeforeAll</c> and initializes new components.</summary>
        Update,
        /// <summary>The fixed-step update phase; runs neither <c>BeforeAll</c> nor <c>AfterAll</c> and skips component initialization.</summary>
        FixedUpdate,
        /// <summary>The late update phase; the only phase that runs <c>AfterAll</c>.</summary>
        LateUpdate
    }
}
