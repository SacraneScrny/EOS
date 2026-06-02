namespace EOS.Profiling
{
    public sealed class NullProfilerBackend : IEosProfilerBackend
    {
        public static readonly NullProfilerBackend Instance = new();

        public void Begin(string label) { }
        public void End() { }
    }
}
