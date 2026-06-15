using System;

using EOS.Entities;

namespace EOS.Extensions
{
    /// <summary>Entity-facing tag operations over <c>World.Tags</c>; tag keys are strings or enum values.</summary>
    public static class TagExtensions
    {
        /// <summary>Adds a single tag (string or enum value) to the entity; no-op on an invalid entity.</summary>
        public static void AddTag(this EosEntity entity, object tag)
        {
            if (entity.IsValid) entity._internal_world.Tags.Add(entity, tag);
        }

        /// <summary>Adds several tags at once to the entity; no-op on an invalid entity.</summary>
        public static void AddTag(this EosEntity entity, params object[] tags)
        {
            if (entity.IsValid) entity._internal_world.Tags.Add(entity, tags);
        }

        /// <summary>Removes a single tag from the entity; no-op on an invalid entity.</summary>
        public static void RemoveTag(this EosEntity entity, object tag)
        {
            if (entity.IsValid) entity._internal_world.Tags.Remove(entity, tag);
        }

        /// <summary>Removes several tags at once from the entity; no-op on an invalid entity.</summary>
        public static void RemoveTag(this EosEntity entity, params object[] tags)
        {
            if (entity.IsValid) entity._internal_world.Tags.Remove(entity, tags);
        }

        /// <summary>Adds the tag when <paramref name="on"/> is true, otherwise removes it.</summary>
        public static void SetFlag(this EosEntity entity, object tag, bool on)
        {
            if (on) entity.AddTag(tag);
            else entity.RemoveTag(tag);
        }

        /// <summary>True when the entity carries the given tag.</summary>
        public static bool HasTag(this EosEntity entity, object tag)
            => entity.IsValid && entity._internal_world.Tags.Has(entity, tag);

        /// <summary>True when the entity carries every one of the given tags.</summary>
        public static bool HasAllTags(this EosEntity entity, params object[] tags)
            => entity.IsValid && entity._internal_world.Tags.HasAll(entity, tags);

        /// <summary>True when the entity carries at least one of the given tags.</summary>
        public static bool HasAnyTag(this EosEntity entity, params object[] tags)
            => entity.IsValid && entity._internal_world.Tags.HasAny(entity, tags);

        /// <summary>True when the entity carries exactly one of the given tags.</summary>
        public static bool HasOneTag(this EosEntity entity, params object[] tags)
            => entity.IsValid && entity._internal_world.Tags.HasOne(entity, tags);

        /// <summary>True when the entity carries any tag belonging to enum type <typeparamref name="TEnum"/>.</summary>
        public static bool HasAnyIn<TEnum>(this EosEntity entity) where TEnum : struct, Enum
            => entity.IsValid && entity._internal_world.Tags.HasAnyIn(entity, typeof(TEnum));

        /// <summary>Removes all tags from the entity.</summary>
        public static void ClearTags(this EosEntity entity)
        {
            if (entity.IsValid) entity._internal_world.Tags.ClearEntity(entity);
        }
    }
}
