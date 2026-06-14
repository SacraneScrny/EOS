using EOS.Core;
using EOS.Objects;

namespace EOS.Queries
{
    /// <summary>Extension entry points for building imperative <see cref="EntityQuery{T}"/> queries over a world from outside the system loop.</summary>
    public static class WorldQueries
    {
        /// <summary>Begins a query over entities carrying a ready <typeparamref name="T"/>.</summary>
        public static EntityQuery<T> Query<T>(this IReadOnlyWorld world)
            where T : EosObject, new()
            => new(world);

        /// <summary>Begins a query over entities carrying both ready components.</summary>
        public static EntityQuery<T1, T2> Query<T1, T2>(this IReadOnlyWorld world)
            where T1 : EosObject, new()
            where T2 : EosObject, new()
            => new(world);

        /// <summary>Begins a query over entities carrying all three ready components.</summary>
        public static EntityQuery<T1, T2, T3> Query<T1, T2, T3>(this IReadOnlyWorld world)
            where T1 : EosObject, new()
            where T2 : EosObject, new()
            where T3 : EosObject, new()
            => new(world);
    }
}
