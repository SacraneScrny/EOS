using EOS.Core;
using EOS.Objects;

namespace EOS.Queries
{
    public static class WorldQueries
    {
        public static EntityQuery<T> Query<T>(this IReadOnlyWorld world)
            where T : EosObject, new()
            => new(world);

        public static EntityQuery<T1, T2> Query<T1, T2>(this IReadOnlyWorld world)
            where T1 : EosObject, new()
            where T2 : EosObject, new()
            => new(world);

        public static EntityQuery<T1, T2, T3> Query<T1, T2, T3>(this IReadOnlyWorld world)
            where T1 : EosObject, new()
            where T2 : EosObject, new()
            where T3 : EosObject, new()
            => new(world);
    }
}
