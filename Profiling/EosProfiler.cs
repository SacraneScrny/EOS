using System;

using EOS.Logging;

namespace EOS.Profiling
{
    public static class EosProfiler
    {
        public static bool Enabled;
        public static IEosProfilerBackend Backend = NullProfilerBackend.Instance;

        public static void Begin(string label)
        {
            if (!Enabled || Backend == null) return;
            try { Backend.Begin(label); }
            catch (Exception ex) { EosLog.Error($"backend Begin('{label}') threw: {ex.Message}", nameof(EosProfiler)); }
        }

        public static void End()
        {
            if (!Enabled || Backend == null) return;
            try { Backend.End(); }
            catch (Exception ex) { EosLog.Error($"backend End threw: {ex.Message}", nameof(EosProfiler)); }
        }

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

        public readonly struct Scope : IDisposable
        {
            readonly IEosProfilerBackend _backend;

            internal Scope(IEosProfilerBackend backend) => _backend = backend;

            public void Dispose()
            {
                if (_backend == null) return;
                try { _backend.End(); }
                catch (Exception ex) { EosLog.Error($"backend End threw: {ex.Message}", nameof(EosProfiler)); }
            }
        }
    }
}
