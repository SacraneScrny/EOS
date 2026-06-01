using System;
using System.Collections.Generic;
using System.Reflection;
using EOS.Core;
using EOS.Entities;
using EOS.Logging;
using EOS.Objects;
using EOS.Serialization.Snapshot;
using EOS.Storage;

namespace EOS.Serialization
{
    public static class WorldSerializer
    {
        static readonly MethodInfo _getMethod = typeof(ObjectsStorageMap)
            .GetMethod(nameof(ObjectsStorageMap.Get), BindingFlags.Instance | BindingFlags.Public);

        public static void Save()
        {
            if (WorldLoader.OnSave == null) return;
            WorldLoader.OnSave(Capture());
        }

        public static UniverseSnapshot Capture()
        {
            var snapshot = new UniverseSnapshot();

            var dw = Universe.InternalDefaultWorld;
            if (dw != null && dw.IsSerializable && !dw.IsDisposed)
                snapshot.Worlds.Add(CaptureWorld(dw));

            foreach (var world in Universe.InternalOtherWorlds)
                if (world.IsSerializable && !world.IsDisposed)
                    snapshot.Worlds.Add(CaptureWorld(world));

            return snapshot;
        }

        static WorldSnapshot CaptureWorld(World world)
        {
            var ws = new WorldSnapshot { WorldKey = world.Key };

            foreach (var entity in world.Entities.All())
            {
                if (!world.Entities.IsSerializable(entity)) continue;
                ws.Entities.Add(new EntityRecord
                {
                    LocalId = entity.Id,
                    Name = entity.Name,
                    Active = entity.IsActive,
                    StableKey = world.Entities.GetStableKey(entity)
                });
            }

            foreach (var (type, storage) in world.ObjectsStorages.AllStorages)
            {
                var indexed = storage as IIndexedStorage;
                if (indexed == null || indexed.Count == 0) continue;

                var bag = new ComponentBag { TypeName = type.AssemblyQualifiedName };

                for (int i = 0; i < indexed.Count; i++)
                {
                    var owner = indexed.GetOwner(i);
                    if (!world.Entities.IsSerializable(owner)) continue;

                    if (indexed.GetAt(i) is IObjectSerializable s)
                    {
                        if (bag.DataTypeName == null)
                            bag.DataTypeName = s.DataType.AssemblyQualifiedName;
                        bag.Items.Add(new ComponentRecord { EntityLocalId = owner.Id, Data = s.SerializeData() });
                    }
                    else
                        bag.Items.Add(new ComponentRecord { EntityLocalId = owner.Id });
                }

                if (bag.Items.Count > 0)
                    ws.Components.Add(bag);
            }

            return ws;
        }

        public static void Restore(UniverseSnapshot snapshot)
        {
            if (snapshot?.Worlds == null) return;

            foreach (var ws in snapshot.Worlds)
            {
                World world;
                if (string.IsNullOrEmpty(ws.WorldKey))
                {
                    world = Universe.InternalDefaultWorld;
                }
                else
                {
                    if (!Universe.TryGetWorld(ws.WorldKey, out world))
                        world = Universe.CreateWorld(ws.WorldKey);
                }

                RestoreWorld(world, ws);
            }
        }

        static void RestoreWorld(World world, WorldSnapshot ws)
        {
            var mapper = new Dictionary<int, EosEntity>(ws.Entities.Count);

            foreach (var record in ws.Entities)
            {
                var entity = new EosEntity(world, record.Name, record.Active);
                mapper[record.LocalId] = entity;
                if (!string.IsNullOrEmpty(record.StableKey))
                    world.Entities.SetStableKey(entity, record.StableKey);
            }

            var ctx = new RestoreContext(mapper, world);

            foreach (var bag in ws.Components)
            {
                var componentType = ResolveType(bag.TypeName);
                if (componentType == null)
                {
                    EosLog.Warning($"Component type '{bag.TypeName}' not found, skipping", nameof(WorldSerializer));
                    continue;
                }

                IStorage storage;
                try
                {
                    storage = (IStorage)_getMethod.MakeGenericMethod(componentType).Invoke(world.ObjectsStorages, null);
                }
                catch (Exception ex)
                {
                    EosLog.Error($"Failed to get storage for '{componentType.Name}': {ex.Message}", nameof(WorldSerializer));
                    continue;
                }

                foreach (var record in bag.Items)
                {
                    if (!mapper.TryGetValue(record.EntityLocalId, out var entity)) continue;

                    EosObject component;
                    try { component = storage.AddObject(entity); }
                    catch (Exception ex)
                    {
                        EosLog.Error($"Failed to add '{componentType.Name}' to entity {record.EntityLocalId}: {ex.Message}", nameof(WorldSerializer));
                        continue;
                    }

                    if (component == null || record.Data == null) continue;

                    if (component is IObjectSerializable s)
                    {
                        component.IsDeserialized = true;
                        try { s.DeserializeData(record.Data, ctx); }
                        catch (Exception ex)
                        {
                            EosLog.Error($"{componentType.Name}.DeserializeData threw: {ex.Message}", nameof(WorldSerializer));
                        }
                    }
                }
            }
        }

        static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            try
            {
                var type = Type.GetType(typeName, ResolveAssembly, ResolveTypeInAssembly);
                if (type != null) return type;
            }
            catch (Exception ex)
            {
                EosLog.Warning($"Type resolve failed for '{typeName}': {ex.Message}", nameof(WorldSerializer));
            }
            return Type.GetType(typeName);
        }

        static Assembly ResolveAssembly(AssemblyName name)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
                if (assemblies[i].GetName().Name == name.Name)
                    return assemblies[i];
            return null;
        }

        static Type ResolveTypeInAssembly(Assembly assembly, string name, bool ignoreCase)
            => assembly != null ? assembly.GetType(name, false, ignoreCase) : Type.GetType(name);

        sealed class RestoreContext : IDeserializeContext
        {
            readonly Dictionary<int, EosEntity> _mapper;
            public World World { get; }

            public RestoreContext(Dictionary<int, EosEntity> mapper, World world)
            {
                _mapper = mapper;
                World = world;
            }

            public EosEntity Resolve(int localId) =>
                _mapper.TryGetValue(localId, out var e) ? e : EosEntity.Null;
        }
    }
}
