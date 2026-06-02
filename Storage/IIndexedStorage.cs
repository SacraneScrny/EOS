using EOS.Entities;

namespace EOS.Storage
{
    public interface IIndexedStorage
    {
        int Count { get; }
        EosEntity GetOwner(int index);
        object GetAt(int index);
        object TryGetObject(EosEntity entity);
        bool IsReady(int index);
        void MarkReady(EosEntity entity);
        void Bump(EosEntity entity);
        int IndexOf(EosEntity entity);
        bool HasReady(EosEntity entity);
        object TryGetReadyObject(EosEntity entity);

        ulong MaxAddVersion { get; }
        ulong MaxMarkVersion { get; }
        ulong AddVersionAt(int index);
        ulong MarkVersionAt(int index);
    }
}