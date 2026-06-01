using System;

using EOS.Entities;

namespace EOS.Extensions
{
    public static class TagExtensions
    {
        public static void AddTag(this EosEntity entity, params object[] tags)
        {
            if (entity.IsValid) entity.World.Tags.Add(entity, tags);
        }

        public static void RemoveTag(this EosEntity entity, params object[] tags)
        {
            if (entity.IsValid) entity.World.Tags.Remove(entity, tags);
        }

        public static void SetFlag(this EosEntity entity, object tag, bool on)
        {
            if (on) entity.AddTag(tag);
            else entity.RemoveTag(tag);
        }

        public static bool HasTag(this EosEntity entity, object tag)
            => entity.IsValid && entity.World.Tags.Has(entity, tag);

        public static bool HasAllTags(this EosEntity entity, params object[] tags)
            => entity.IsValid && entity.World.Tags.HasAll(entity, tags);

        public static bool HasAnyTag(this EosEntity entity, params object[] tags)
            => entity.IsValid && entity.World.Tags.HasAny(entity, tags);

        public static bool HasOneTag(this EosEntity entity, params object[] tags)
            => entity.IsValid && entity.World.Tags.HasOne(entity, tags);

        public static bool HasAnyIn<TEnum>(this EosEntity entity) where TEnum : struct, Enum
            => entity.IsValid && entity.World.Tags.HasAnyIn(entity, typeof(TEnum));

        public static void ClearTags(this EosEntity entity)
        {
            if (entity.IsValid) entity.World.Tags.ClearEntity(entity);
        }
    }
}
