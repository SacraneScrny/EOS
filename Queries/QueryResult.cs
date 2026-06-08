using EOS.Entities;
using EOS.Objects;

namespace EOS.Queries
{
    public readonly struct QueryResult<T1, T2>
        where T1 : EosObject
        where T2 : EosObject
    {
        public readonly EosEntity Entity;
        public readonly T1 Item1;
        public readonly T2 Item2;

        public QueryResult(EosEntity entity, T1 item1, T2 item2)
        {
            Entity = entity;
            Item1 = item1;
            Item2 = item2;
        }

        public void Deconstruct(out T1 item1, out T2 item2)
        {
            item1 = Item1;
            item2 = Item2;
        }

        public void Deconstruct(out EosEntity entity, out T1 item1, out T2 item2)
        {
            entity = Entity;
            item1 = Item1;
            item2 = Item2;
        }
    }

    public readonly struct QueryResult<T1, T2, T3>
        where T1 : EosObject
        where T2 : EosObject
        where T3 : EosObject
    {
        public readonly EosEntity Entity;
        public readonly T1 Item1;
        public readonly T2 Item2;
        public readonly T3 Item3;

        public QueryResult(EosEntity entity, T1 item1, T2 item2, T3 item3)
        {
            Entity = entity;
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
        }

        public void Deconstruct(out T1 item1, out T2 item2, out T3 item3)
        {
            item1 = Item1;
            item2 = Item2;
            item3 = Item3;
        }

        public void Deconstruct(out EosEntity entity, out T1 item1, out T2 item2, out T3 item3)
        {
            entity = Entity;
            item1 = Item1;
            item2 = Item2;
            item3 = Item3;
        }
    }
}
