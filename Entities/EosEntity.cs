using System;

namespace EOS.Entities
{
    public readonly struct EosEntity : IEquatable<EosEntity>
    {
        public static readonly EosEntity Null = new(-1, 0, string.Empty);

        public readonly int Id;
        public readonly ushort Version;
        public readonly string Name;

        public bool IsValid => EntitiesContainer.IsValid(this);
        public bool IsActive => EntitiesContainer.IsActive(this);

        public EosEntity(string name = "", bool active = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Entity";

            (Id, Version, Name) = EntitiesContainer.Create(name, active);
        }
        internal EosEntity(int id, ushort version, string name = "")
        {
            Id = id;
            Version = version;
            Name = name;
        }

        public void Destroy() => EntitiesContainer.Destroy(this);

        public static implicit operator int(EosEntity entity) => entity.Id;

        public static bool operator ==(EosEntity a, EosEntity b) => a.Id == b.Id && a.Version == b.Version;
        public static bool operator !=(EosEntity a, EosEntity b) => !(a == b);
        public bool Equals(EosEntity other) => Id == other.Id && Version == other.Version;
        public override bool Equals(object obj) => obj is EosEntity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Version);
    }
}
