using System;

using EOS.Core;

namespace EOS.Entities
{
    /// <summary>Immutable handle to an entity: id + version + owning world. Stale handles (after destroy) are detectable via the version.</summary>
    public readonly struct EosEntity : IEquatable<EosEntity>
    {
        /// <summary>The invalid/sentinel handle (<c>Id = -1</c>, <c>Version = 0</c>, no world); never refers to a live entity.</summary>
        public static readonly EosEntity Null = new(-1, 0, null);

        /// <summary>The entity's id; index into the world's entity arrays.</summary>
        public readonly int Id;
        /// <summary>Version stamp incremented on destroy; mismatch marks this handle stale.</summary>
        public readonly ushort Version;
        internal readonly World World;

        /// <summary>The entity's name, resolved from the world; empty if the handle is detached or invalid.</summary>
        public string Name => World != null ? World.Entities.GetName(this) : string.Empty;

        /// <summary>True while this handle refers to a live entity (exists and version matches).</summary>
        public bool IsValid => World != null && World.Entities.IsValid(this);
        /// <summary>Effective active state: this entity's own flag AND every ancestor's.</summary>
        public bool IsActive => World != null && World.Entities.IsActive(this);
        /// <summary>This entity's own active flag alone, independent of ancestors.</summary>
        public bool IsActiveSelf => World != null && World.Entities.IsActiveSelf(this);

        /// <summary>Creates a new entity in <paramref name="world"/> (inactive by default; blank names normalize to "Entity").</summary>
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

        /// <summary>Destroys this entity (and cascades to its children).</summary>
        public void Destroy() => World.Entities.Destroy(this);

        /// <summary>Equality over id, version and world id.</summary>
        public static bool operator ==(EosEntity a, EosEntity b) => a.Id == b.Id && a.Version == b.Version && (a.World?.Id ?? -1) == (b.World?.Id ?? -1);
        /// <summary>Inequality over id, version and world id.</summary>
        public static bool operator !=(EosEntity a, EosEntity b) => !(a == b);
        /// <summary>True when id, version and world id all match.</summary>
        public bool Equals(EosEntity other) => Id == other.Id && Version == other.Version && (World?.Id ?? -1) == (other.World?.Id ?? -1);
        /// <summary>True when <paramref name="obj"/> is an <see cref="EosEntity"/> equal to this one.</summary>
        public override bool Equals(object obj) => obj is EosEntity other && Equals(other);
        /// <summary>Hash combining id, version and world id.</summary>
        public override int GetHashCode() => HashCode.Combine(Id, Version, World?.Id ?? -1);
    }
}
