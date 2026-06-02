using System;
using System.Collections.Generic;
using System.Text;

using EOS.Core;
using EOS.Entities;
using EOS.Storage;

namespace EOS.Diagnostics
{
    public static class WorldDebug
    {
        public static string DumpUniverse()
        {
            var sb = new StringBuilder();
            if (Universe.DefaultWorld != null)
                AppendWorld(sb, Universe.DefaultWorld);

            foreach (var world in Universe.OtherWorlds)
            {
                sb.AppendLine();
                AppendWorld(sb, world);
            }
            return sb.ToString();
        }

        public static string DumpWorld(IReadOnlyWorld world)
        {
            if (world == null) return string.Empty;
            var sb = new StringBuilder();
            AppendWorld(sb, world);
            return sb.ToString();
        }

        public static string DumpEntity(EosEntity entity)
        {
            var sb = new StringBuilder();
            AppendEntity(sb, entity, entity.World, new List<string>());
            return sb.ToString();
        }

        static void AppendWorld(StringBuilder sb, IReadOnlyWorld world)
        {
            sb.Append("World #").Append(world.Id);
            if (!string.IsNullOrEmpty(world.Key)) sb.Append(" '").Append(world.Key).Append('\'');
            sb.Append(world.IsEnabled ? " [enabled]" : " [disabled]");
            if (world.IsDisposed) sb.Append(" [disposed]");
            sb.Append(" frame=").Append(world.Frame);
            sb.Append(" version=").Append(world.Version);
            sb.AppendLine();

            int entityCount = 0;
            foreach (var _ in world.Entities.All()) entityCount++;

            int systemCount = 0;
            foreach (var _ in world.Systems.All) systemCount++;

            sb.Append("entities: ").Append(entityCount)
              .Append("  systems: ").Append(systemCount).AppendLine();

            sb.AppendLine("component counts:");
            foreach (var kv in world.ObjectsStorages.AllStorages)
            {
                int count = kv.Value is IIndexedStorage indexed ? indexed.Count : 0;
                if (count == 0) continue;
                sb.Append("  ").Append(TypeName(kv.Key)).Append(": ").Append(count).AppendLine();
            }

            sb.AppendLine("entities:");
            var tagNames = new List<string>();
            foreach (var entity in world.Entities.All())
                AppendEntity(sb, entity, world, tagNames);
        }

        static void AppendEntity(StringBuilder sb, EosEntity entity, IReadOnlyWorld world, List<string> tagNames)
        {
            sb.Append("Entity #").Append(entity.Id)
              .Append(" v").Append(entity.Version)
              .Append(" '").Append(entity.Name ?? string.Empty).Append('\'');

            if (world == null)
            {
                sb.Append(" [detached]").AppendLine();
                return;
            }

            sb.Append(entity.IsActive ? " [active]" : " [inactive]");
            if (!entity.IsValid) sb.Append(" [stale]");

            var key = world.Entities.GetStableKey(entity);
            if (!string.IsNullOrEmpty(key)) sb.Append(" key=").Append(key);
            sb.AppendLine();

            sb.Append("  components: ");
            bool anyComponent = false;
            foreach (var kv in world.ObjectsStorages.AllStorages)
            {
                if (kv.Value is IIndexedStorage indexed && indexed.TryGetObject(entity) != null)
                {
                    if (anyComponent) sb.Append(", ");
                    sb.Append(TypeName(kv.Key));
                    anyComponent = true;
                }
            }
            if (!anyComponent) sb.Append("(none)");
            sb.AppendLine();

            world.Tags.GetTagNames(entity, tagNames);
            sb.Append("  tags: ");
            if (tagNames.Count == 0) sb.Append("(none)");
            else
                for (int i = 0; i < tagNames.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(tagNames[i]);
                }
            sb.AppendLine();
        }

        static string TypeName(Type type)
        {
            if (!type.IsGenericType) return type.Name;

            var sb = new StringBuilder();
            var name = type.Name;
            int tick = name.IndexOf('`');
            sb.Append(tick >= 0 ? name.Substring(0, tick) : name);
            sb.Append('<');
            var args = type.GetGenericArguments();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(TypeName(args[i]));
            }
            sb.Append('>');
            return sb.ToString();
        }
    }
}
