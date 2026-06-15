using System;
using System.Collections.Generic;

using EOS.Core;
using EOS.Logging;

namespace EOS.Events
{
    /// <summary>Per-world owner of all <see cref="EventChannel{T}"/> instances; emits events and drives their per-phase promote/trim lifecycle.</summary>
    public sealed class EventsContainer : WorldBound
    {
        readonly Dictionary<Type, IEventChannel> _channels = new();

        /// <summary>Hard cap in frames on how long a live event survives, so an undriven phase cannot leak unread events.</summary>
        public const ulong MaxAge = 16;

        EventChannel<T> Get<T>() where T : struct
        {
            if (_channels.TryGetValue(typeof(T), out var existing))
                return (EventChannel<T>)existing;

            var created = new EventChannel<T>();
            _channels.Add(typeof(T), created);
            return created;
        }

        /// <summary>Stages an event of type <typeparamref name="T"/> for emission; safe mid-iteration since it is not a structural change.</summary>
        public void Enqueue<T>(in T e) where T : struct => Get<T>().Enqueue(e);

        /// <summary>Returns the channel for event type <typeparamref name="T"/>, creating it on first access.</summary>
        public EventChannel<T> Channel<T>() where T : struct => Get<T>();

        internal IEventChannel ChannelFor(Type eventType)
        {
            if (_channels.TryGetValue(eventType, out var existing))
                return existing;

            try
            {
                var created = (IEventChannel)Activator.CreateInstance(typeof(EventChannel<>).MakeGenericType(eventType));
                _channels.Add(eventType, created);
                return created;
            }
            catch (Exception ex)
            {
                EosLog.Error($"Failed to create event channel for {eventType.Name}: {ex.Message}", nameof(EventsContainer));
                return null;
            }
        }

        internal void Promote()
        {
            foreach (var channel in _channels.Values)
            {
                try { channel.Promote(World.Frame); }
                catch (Exception ex) { EosLog.Error($"Promote failed: {ex.Message}", nameof(EventsContainer)); }
            }
        }
        internal void Trim()
        {
            foreach (var channel in _channels.Values)
            {
                try { channel.Trim(World.Frame, World.ReactiveRetentionFrames); }
                catch (Exception ex) { EosLog.Error($"Trim failed: {ex.Message}", nameof(EventsContainer)); }
            }
        }

        internal void Reset()
        {
            foreach (var channel in _channels.Values)
                channel.Clear();
        }
    }
}
