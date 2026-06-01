using EOS.Entities;

namespace EOS.Loader
{
    public interface IIncarnationBinder<TView> where TView : class
    {
        TView Instantiate(EosEntity entity, string incarnationId);
        void Destroy(EosEntity entity, TView view);
        void Sync(EosEntity entity, TView view);
        void SyncFixed(EosEntity entity, TView view);
        void SyncLate(EosEntity entity, TView view);
    }
}
