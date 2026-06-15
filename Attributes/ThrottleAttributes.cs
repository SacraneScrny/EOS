using System;

namespace EOS.Attributes
{
    /// <summary>Caps an <c>Execute</c> to at most <see cref="MaxPerFrame"/> matched entities per frame; the remainder roll over to following frames (time-slicing) rather than being dropped. Works on continuous and reactive queries.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class BudgetAttribute : Attribute
    {
        /// <summary>The maximum number of matched entities processed per frame.</summary>
        public readonly int MaxPerFrame;
        /// <summary>Caps the query to <paramref name="maxPerFrame"/> matched entities per frame, deferring the rest to later frames.</summary>
        public BudgetAttribute(int maxPerFrame) => MaxPerFrame = maxPerFrame;
    }

    /// <summary>Runs an <c>Execute</c> at most once per <see cref="Seconds"/> of accumulated phase delta time (coroutine-like cadence) instead of every frame. Fires immediately on the first eligible frame, then waits the interval; reactive edges accumulated while asleep are still delivered on the next run.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DelayAttribute : Attribute
    {
        /// <summary>The interval in seconds between runs.</summary>
        public readonly float Seconds;
        /// <summary>Throttles the query to run once per <paramref name="seconds"/> of accumulated delta time.</summary>
        public DelayAttribute(float seconds) => Seconds = seconds;
    }

    /// <summary>Runs an <c>Execute</c> once every <see cref="Frames"/> phase invocations instead of every frame. Fires immediately on the first eligible frame, then waits the interval; reactive edges accumulated while asleep are still delivered on the next run.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DelayFrameAttribute : Attribute
    {
        /// <summary>The interval in phase frames between runs.</summary>
        public readonly int Frames;
        /// <summary>Throttles the query to run once every <paramref name="frames"/> phase invocations.</summary>
        public DelayFrameAttribute(int frames) => Frames = frames;
    }
}
