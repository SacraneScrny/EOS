using System.Collections.Generic;

using EOS.Attributes;
using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Systems;
using Xunit;

namespace EOS.Tests
{
    public sealed class OrderProbe : EosObject { public readonly List<string> Log = new(); }
    public sealed class OrderNumProbe : EosObject { public readonly List<string> Log = new(); }
    public sealed class OrderBeforeProbe : EosObject { public readonly List<string> Log = new(); }

    public sealed class OrderTarget : EosSystem
    {
        void Execute(OrderBeforeProbe p) => p.Log.Add("target");
    }

    [UpdateBefore(typeof(OrderTarget))]
    public sealed class OrderBeforeSys : EosSystem
    {
        void Execute(OrderBeforeProbe p) => p.Log.Add("before");
    }

    public sealed class OrderA : EosSystem
    {
        void Execute(OrderProbe p) => p.Log.Add("A");
    }

    [UpdateAfter(typeof(OrderA))]
    public sealed class OrderB : EosSystem
    {
        void Execute(OrderProbe p) => p.Log.Add("B");
    }

    [UpdateAfter(typeof(OrderB))]
    public sealed class OrderC : EosSystem
    {
        void Execute(OrderProbe p) => p.Log.Add("C");
    }

    [UpdateOrder(10)]
    public sealed class OrderLate : EosSystem
    {
        void Execute(OrderNumProbe p) => p.Log.Add("late");
    }

    [UpdateOrder(-10)]
    public sealed class OrderEarly : EosSystem
    {
        void Execute(OrderNumProbe p) => p.Log.Add("early");
    }

    public sealed class SystemOrderingTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void UpdateAfter_ChainsExecutionOrder()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<OrderProbe>();

            world.Update(0f);

            Assert.Equal(new[] { "A", "B", "C" }, e.Get<OrderProbe>().Log.ToArray());
        }

        [Fact]
        public void UpdateOrder_NumericOrdersSystems()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<OrderNumProbe>();

            world.Update(0f);

            Assert.Equal(new[] { "early", "late" }, e.Get<OrderNumProbe>().Log.ToArray());
        }

        [Fact]
        public void UpdateBefore_OrdersExecution()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            e.Add<OrderBeforeProbe>();

            world.Update(0f);

            Assert.Equal(new[] { "before", "target" }, e.Get<OrderBeforeProbe>().Log.ToArray());
        }
    }
}
