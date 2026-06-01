using System;

using EOS.Core;
using EOS.Entities;
using EOS.Extensions;
using EOS.Objects;
using EOS.Objects.Interfaces;
using Xunit;

namespace EOS.Tests
{
    public sealed class TrackedDisposable : IDisposable
    {
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }

    public sealed class TracingProbe : EosObject
    {
        public readonly TrackedDisposable First = new();
        public readonly TrackedDisposable Second = new();
        protected override void OnAwake() => Trace(First, Second);
    }

    public sealed class LifecycleProbe : EosObject
    {
        public int Awakes;
        public int Starts;
        public int Disposes;
        protected override void OnAwake() => Awakes++;
        protected override void OnStart() => Starts++;
        protected override void OnDispose() => Disposes++;
    }

    public sealed class UpdatingProbe : EosObject, IObjectUpdate, IObjectFixedUpdate, IObjectLateUpdate
    {
        public int Updates;
        public int FixedUpdates;
        public int LateUpdates;
        public void OnUpdate(float deltaTime) => Updates++;
        public void OnFixedUpdate(float deltaTime) => FixedUpdates++;
        public void OnLateUpdate(float deltaTime) => LateUpdates++;
    }

    public sealed class ObjectLifecycleTests
    {
        static World NewWorld()
        {
            var world = new World();
            world.Init();
            return world;
        }

        [Fact]
        public void AwakeAndStart_RunOnceOnFirstUpdate()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            var probe = e.Add<LifecycleProbe>();

            Assert.False(probe.IsAwaken);
            Assert.False(probe.IsEnabled);

            world.Update(0f);

            Assert.True(probe.IsAwaken);
            Assert.True(probe.IsStarted);
            Assert.True(probe.IsEnabled);
            Assert.Equal(1, probe.Awakes);
            Assert.Equal(1, probe.Starts);

            world.Update(0f);
            Assert.Equal(1, probe.Awakes);
            Assert.Equal(1, probe.Starts);
        }

        [Fact]
        public void Dispose_RunsOnRemove()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            var probe = e.Add<LifecycleProbe>();
            world.Update(0f);

            e.Remove<LifecycleProbe>();

            Assert.True(probe.IsDisposed);
            Assert.Equal(1, probe.Disposes);
        }

        [Fact]
        public void InactiveEntity_DefersAwakeUntilActivated()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", active: false);
            var probe = e.Add<LifecycleProbe>();

            world.Update(0f);
            Assert.Equal(0, probe.Awakes);

            e.On();
            world.Update(0f);
            Assert.Equal(1, probe.Awakes);
        }

        [Fact]
        public void UpdateInterfaces_FireForEnabledObjects()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            var probe = e.Add<UpdatingProbe>();

            world.Update(0f);
            world.FixedUpdate(0f);
            world.LateUpdate(0f);

            Assert.Equal(1, probe.Updates);
            Assert.Equal(1, probe.FixedUpdates);
            Assert.Equal(1, probe.LateUpdates);
        }

        [Fact]
        public void TracedDisposables_AreDisposedWithObject()
        {
            var world = NewWorld();
            var e = new EosEntity(world, "e", true);
            var probe = e.Add<TracingProbe>();
            world.Update(0f);

            e.Remove<TracingProbe>();

            Assert.True(probe.First.Disposed);
            Assert.True(probe.Second.Disposed);
        }
    }
}
