using EOS.Entities;
using EOS.Objects;

namespace EOS.Storage
{
    /// <summary>Non-generic structural surface of a <see cref="Storage{T}"/>, used by entity-destroy and deserialization paths that don't know the component type.</summary>
    public interface IStorage
    {
        /// <summary>Removes and disposes the entity's component, if present.</summary>
        void RemoveEntity(EosEntity entity);
        /// <summary>Disposes every component and drains any pool.</summary>
        void Clear();
        /// <summary>Adds (or returns the existing) component for the entity and returns it as an <see cref="EosObject"/>.</summary>
        EosObject AddObject(EosEntity entity);
    }
}