using System;

using EOS.Core;

namespace EOS.Entities
{
    public readonly struct EosEntity : IEquatable<EosEntity>
    {
        public static readonly EosEntity Null = new(-1, 0, null, string.Empty);

        public readonly int Id;
        public readonly ushort Version;
        public readonly string Name;
        internal readonly World World;

        public bool IsValid => World != null && World.Entities.IsValid(this);
        public bool IsActive => World != null && World.Entities.IsActive(this);

        public EosEntity(World world, string name = "", bool active = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Entity";
            World = world;

            (Id, Version, Name) = world.Entities.Create(name, active);
        }
        internal EosEntity(int id, ushort version, World world, string name = "")
        {
            Id = id;
            Version = version;
            Name = name;
            World = world;
        }

        public void Destroy() => World.Entities.Destroy(this);

        public static implicit operator int(EosEntity entity) => entity.Id;

        public static bool operator ==(EosEntity a, EosEntity b) => a.Id == b.Id && a.Version == b.Version;
        public static bool operator !=(EosEntity a, EosEntity b) => !(a == b);
        public bool Equals(EosEntity other) => Id == other.Id && Version == other.Version;
        public override bool Equals(object obj) => obj is EosEntity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Version);
    }
}
