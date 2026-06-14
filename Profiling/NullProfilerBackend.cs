namespace EOS.Profiling
{
    /// <summary>No-op profiler backend used as the zero-overhead default.</summary>
    public sealed class NullProfilerBackend : IEosProfilerBackend
    {
        /// <summary>Shared singleton instance.</summary>
        public static readonly NullProfilerBackend Instance = new();

        /// <summary>Does nothing.</summary>
        public void Begin(string label) { }
        /// <summary>Does nothing.</summary>
        public void End() { }
    }
}
