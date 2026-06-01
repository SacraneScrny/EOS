using EOS.Entities;

namespace EOS.Loader
{
    public interface IIncarnationBinder
    {
        object Instantiate(EosEntity entity, string incarnationId);
        void Destroy(EosEntity entity, object view);
        void Sync(EosEntity entity, object view);
        void SyncFixed(EosEntity entity, object view);
        void SyncLate(EosEntity entity, object view);
    }
}
