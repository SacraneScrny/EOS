using EOS.Core;
using EOS.Entities;

namespace EOS.Serialization
{
    /// <summary>Deserialization context passed to <see cref="IObjectSerializable.DeserializeData"/>; lets a component resolve stored local ids back to live entities and reach the target world.</summary>
    public interface IDeserializeContext
    {
        /// <summary>Maps a snapshot-local entity id to the live <see cref="EosEntity"/> created during restore; returns <see cref="EosEntity.Null"/> if unmapped.</summary>
        EosEntity Resolve(int localId);
        /// <summary>The world the snapshot is being restored into.</summary>
        World World { get; }
    }
}
