using EOS.Entities;

namespace EOS.Loader
{
    /// <summary>Consumer-supplied bridge that instantiates, syncs and destroys an external view of type <typeparamref name="TView"/> for an incarnated entity; register via <see cref="IncarnationBridge"/>.</summary>
    public interface IIncarnationBinder<TView> where TView : class
    {
        /// <summary>Creates the view for <paramref name="entity"/> from the given incarnation id.</summary>
        TView Instantiate(EosEntity entity, string incarnationId);
        /// <summary>Destroys the view previously created for <paramref name="entity"/>.</summary>
        void Destroy(EosEntity entity, TView view);
        /// <summary>Syncs entity state into the view during the Update phase.</summary>
        void Sync(EosEntity entity, TView view);
        /// <summary>Syncs entity state into the view during the FixedUpdate phase.</summary>
        void SyncFixed(EosEntity entity, TView view);
        /// <summary>Syncs entity state into the view during the LateUpdate phase.</summary>
        void SyncLate(EosEntity entity, TView view);
    }
}
