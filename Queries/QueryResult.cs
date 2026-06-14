using EOS.Entities;
using EOS.Objects;

namespace EOS.Queries
{
    /// <summary>A two-component query match: the owning entity and its two components, deconstructable as a tuple.</summary>
    public readonly struct QueryResult<T1, T2>
        where T1 : EosObject
        where T2 : EosObject
    {
        /// <summary>The matched entity.</summary>
        public readonly EosEntity Entity;
        /// <summary>The first matched component.</summary>
        public readonly T1 Item1;
        /// <summary>The second matched component.</summary>
        public readonly T2 Item2;

        /// <summary>Creates a result for the given entity and components.</summary>
        public QueryResult(EosEntity entity, T1 item1, T2 item2)
        {
            Entity = entity;
            Item1 = item1;
            Item2 = item2;
        }

        /// <summary>Deconstructs into the two components.</summary>
        public void Deconstruct(out T1 item1, out T2 item2)
        {
            item1 = Item1;
            item2 = Item2;
        }

        /// <summary>Deconstructs into the entity and its two components.</summary>
        public void Deconstruct(out EosEntity entity, out T1 item1, out T2 item2)
        {
            entity = Entity;
            item1 = Item1;
            item2 = Item2;
        }
    }

    /// <summary>A three-component query match: the owning entity and its three components, deconstructable as a tuple.</summary>
    public readonly struct QueryResult<T1, T2, T3>
        where T1 : EosObject
        where T2 : EosObject
        where T3 : EosObject
    {
        /// <summary>The matched entity.</summary>
        public readonly EosEntity Entity;
        /// <summary>The first matched component.</summary>
        public readonly T1 Item1;
        /// <summary>The second matched component.</summary>
        public readonly T2 Item2;
        /// <summary>The third matched component.</summary>
        public readonly T3 Item3;

        /// <summary>Creates a result for the given entity and components.</summary>
        public QueryResult(EosEntity entity, T1 item1, T2 item2, T3 item3)
        {
            Entity = entity;
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
        }

        /// <summary>Deconstructs into the three components.</summary>
        public void Deconstruct(out T1 item1, out T2 item2, out T3 item3)
        {
            item1 = Item1;
            item2 = Item2;
            item3 = Item3;
        }

        /// <summary>Deconstructs into the entity and its three components.</summary>
        public void Deconstruct(out EosEntity entity, out T1 item1, out T2 item2, out T3 item3)
        {
            entity = Entity;
            item1 = Item1;
            item2 = Item2;
            item3 = Item3;
        }
    }
}
