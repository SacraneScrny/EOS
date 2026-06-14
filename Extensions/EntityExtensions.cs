using EOS.Entities;
using EOS.Objects;
using EOS.Storage;

namespace EOS.Extensions
{
    /// <summary>Entity-facing component access (add/get/has/remove/bump) and active-state toggles over the owning world's storages.</summary>
    public static class EntityExtensions
    {
        /// <summary>Adds a fresh component of type <typeparamref name="T"/> to the entity and returns it; null on an invalid entity.</summary>
        public static T Add<T>(this EosEntity entity) where T : EosObject, new()
            => entity.IsValid ? entity.World?.ObjectsStorages.Get<T>().Add(entity) : null;
        /// <summary>Returns the entity's component of type <typeparamref name="T"/>, or null if absent or invalid.</summary>
        public static T Get<T>(this EosEntity entity) where T : EosObject, new()
            => entity.IsValid ? entity.World?.ObjectsStorages.Get<T>().Get(entity) : null;

        /// <summary>Tries to get the entity's component of type <typeparamref name="T"/>; returns false (and null) if absent.</summary>
        public static bool TryGet<T>(this EosEntity entity, out T result) where T : EosObject, new()
        {
            result = null;
            return entity.IsValid && entity.World.ObjectsStorages.Get<T>().TryGet(entity, out result);
        }

        /// <summary>True when the entity carries a component of type <typeparamref name="T"/>.</summary>
        public static bool Has<T>(this EosEntity entity) where T : EosObject, new()
            => entity.IsValid && entity.World.ObjectsStorages.Get<T>().Has(entity);

        /// <summary>Removes and disposes the entity's component of type <typeparamref name="T"/>; returns false if it was absent.</summary>
        public static bool Remove<T>(this EosEntity entity) where T : EosObject, new()
            => entity.IsValid && entity.World.ObjectsStorages.Get<T>().Remove(entity);

        /// <summary>Signals the <c>[Bumped]</c> reactive channel for the entity's <typeparamref name="T"/> (deduped to once per frame).</summary>
        public static void Bump<T>(this EosEntity entity) where T : EosObject, new()
        {
            if (entity.IsValid) entity.World.ObjectsStorages.Get<T>().Bump(entity);
        }

        /// <summary>Activates the entity (sets its self-active flag true); effective state still depends on ancestors.</summary>
        public static void On(this EosEntity entity) => entity.World?.Entities.SetActive(entity, true);
        /// <summary>Deactivates the entity (sets its self-active flag false), suspending its whole subtree.</summary>
        public static void Off(this EosEntity entity) => entity.World?.Entities.SetActive(entity, false);
    }
}
