using EOS.Entities;
using EOS.Objects;

namespace EOS.Storage
{
    public interface IStorage
    {
        void RemoveEntity(EosEntity entity);
        void Clear();
        EosObject AddObject(EosEntity entity);
    }
}