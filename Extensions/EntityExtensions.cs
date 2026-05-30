using EOS.Entities;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Extensions
{
    public static class EntityExtensions
    {
        public static T Add<T>(this EosEntity entity) where T : EosObject, new()
            => StorageMap.Get<T>().Add(entity);

        public static T Get<T>(this EosEntity entity) where T : EosObject, new()
            => StorageMap.Get<T>().Get(entity);

        public static bool TryGet<T>(this EosEntity entity, out T result) where T : EosObject, new()
            => StorageMap.Get<T>().TryGet(entity, out result);

        public static bool Has<T>(this EosEntity entity) where T : EosObject, new()
            => StorageMap.Get<T>().Has(entity);

        public static bool Remove<T>(this EosEntity entity) where T : EosObject, new()
            => StorageMap.Get<T>().Remove(entity);

        public static void On(this EosEntity entity) => EntitiesContainer.SetActive(entity, true);
        public static void Off(this EosEntity entity) => EntitiesContainer.SetActive(entity, false);
    }
}
