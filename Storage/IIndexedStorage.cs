using System.Collections.Generic;

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
        int IndexOf(EosEntity entity);
        bool IsRecent(EosEntity entity);
        IReadOnlyList<EosEntity> RecentlyAdded { get; }
    }
}
