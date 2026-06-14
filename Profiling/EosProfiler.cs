using System;

using EOS.Logging;

namespace EOS.Profiling
{
    /// <summary>Static facade over a swappable profiler backend; off by default and zero-overhead until <see cref="Enabled"/> is set.</summary>
    public static class EosProfiler
    {
        /// <summary>Gates all profiling; when false, <c>Begin</c>/<c>End</c>/<c>Sample</c> are no-ops.</summary>
        public static bool Enabled;
        /// <summary>The backend that receives span begin/end calls; defaults to the no-op null backend.</summary>
        public static IEosProfilerBackend Backend = NullProfilerBackend.Instance;

        /// <summary>Begins a profiling span with the given label (no-op when disabled; backend exceptions are caught and logged).</summary>
        public static void Begin(string label)
        {
            if (!Enabled || Backend == null) return;
            try { Backend.Begin(label); }
            catch (Exception ex) { EosLog.Error($"backend Begin('{label}') threw: {ex.Message}", nameof(EosProfiler)); }
        }

        /// <summary>Ends the most recently begun span (no-op when disabled; backend exceptions are caught and logged).</summary>
        public static void End()
        {
            if (!Enabled || Backend == null) return;
            try { Backend.End(); }
            catch (Exception ex) { EosLog.Error($"backend End threw: {ex.Message}", nameof(EosProfiler)); }
        }

        /// <summary>Begins a span and returns a <see cref="Scope"/> whose <c>Dispose</c> ends it; use with <c>using</c> for balanced timing even if the body throws.</summary>
        public static Scope Sample(string label)
        {
            if (!Enabled || Backend == null) return default;
            try { Backend.Begin(label); }
            catch (Exception ex)
            {
                EosLog.Error($"backend Begin('{label}') threw: {ex.Message}", nameof(EosProfiler));
                return default;
            }
            return new Scope(Backend);
        }

        /// <summary>Disposable span handle returned by <see cref="Sample"/>; ends the span on <see cref="Dispose"/>.</summary>
        public readonly struct Scope : IDisposable
        {
            readonly IEosProfilerBackend _backend;

            internal Scope(IEosProfilerBackend backend) => _backend = backend;

            /// <summary>Ends the span this scope opened (backend exceptions are caught and logged).</summary>
            public void Dispose()
            {
                if (_backend == null) return;
                try { _backend.End(); }
                catch (Exception ex) { EosLog.Error($"backend End threw: {ex.Message}", nameof(EosProfiler)); }
            }
        }
    }
}
