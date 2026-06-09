using System;

using EOS.Core;

namespace EOS.Entities
{
    public readonly struct EosEntity : IEquatable<EosEntity>
    {
        public static readonly EosEntity Null = new(-1, 0, null);

        public readonly int Id;
        public readonly ushort Version;
        internal readonly World World;

        public string Name => World != null ? World.Entities.GetName(this) : string.Empty;

        public bool IsValid => World != null && World.Entities.IsValid(this);
        public bool IsActive => World != null && World.Entities.IsActive(this);

        public EosEntity(World world, string name = "", bool active = false, bool isSerializable = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Entity";
            World = world;

            var created = world.Entities.Create(name, active, isSerializable);
            Id = created.Id;
            Version = created.Version;
        }
        internal EosEntity(int id, ushort version, World world)
        {
            Id = id;
            Version = version;
            World = world;
        }

        public void Destroy() => World.Entities.Destroy(this);

        public static implicit operator int(EosEntity entity) => entity.Id;

        public static bool operator ==(EosEntity a, EosEntity b) => a.Id == b.Id && a.Version == b.Version && (a.World?.Id ?? -1) == (b.World?.Id ?? -1);
        public static bool operator !=(EosEntity a, EosEntity b) => !(a == b);
        public bool Equals(EosEntity other) => Id == other.Id && Version == other.Version && (World?.Id ?? -1) == (other.World?.Id ?? -1);
        public override bool Equals(object obj) => obj is EosEntity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Version, World?.Id ?? -1);
    }
}
