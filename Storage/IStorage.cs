using EOS.Entities;

namespace EOS.Storage
{
    public interface IStorage
    {
        void RemoveEntity(EosEntity entity);
        void Clear();
    }
}