using EOS.Entities;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Extensions
{
    public static class EntityExtensions
    {
        public static T Add<T>(this EosEntity entity) where T : EosObject, new()
            => entity.World?.ObjectsStorages.Get<T>().Add(entity);

        public static T Get<T>(this EosEntity entity) where T : EosObject, new()
            => entity.World?.ObjectsStorages.Get<T>().Get(entity);

        public static bool TryGet<T>(this EosEntity entity, out T result) where T : EosObject, new()
        {
            result = null;
            return entity.IsValid && entity.World.ObjectsStorages.Get<T>().TryGet(entity, out result);
        }
        
        public static bool Has<T>(this EosEntity entity) where T : EosObject, new()
            => entity.IsValid && entity.World.ObjectsStorages.Get<T>().Has(entity);

        public static bool Remove<T>(this EosEntity entity) where T : EosObject, new()
            => entity.IsValid && entity.World.ObjectsStorages.Get<T>().Remove(entity);

        public static void Bump<T>(this EosEntity entity) where T : EosObject, new()
        {
            if (entity.IsValid) entity.World.ObjectsStorages.Get<T>().Bump(entity);
        }

        public static void On(this EosEntity entity) => entity.World?.Entities.SetActive(entity, true);
        public static void Off(this EosEntity entity) => entity.World?.Entities.SetActive(entity, false);
    }
}
