namespace EOS.Objects
{
    /// <summary>Non-generic view-bound interface implemented by <see cref="Incarnation{TView}"/>, letting sync systems drive views without knowing the view type.</summary>
    public interface IIncarnation
    {
        /// <summary>The view id of this incarnation.</summary>
        string Id { get; }
        /// <summary>Dispatches the Update-phase sync to the typed binder.</summary>
        void Sync();
        /// <summary>Dispatches the FixedUpdate-phase sync to the typed binder.</summary>
        void SyncFixed();
        /// <summary>Dispatches the LateUpdate-phase sync to the typed binder.</summary>
        void SyncLate();
    }
}
