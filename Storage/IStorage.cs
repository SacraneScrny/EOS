using EOS.Entities;

namespace EOS.Storage
{
    internal interface IStorage
    {
        void RemoveEntity(EosEntity entity);
        void Clear();
    }
}
