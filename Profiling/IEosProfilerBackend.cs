namespace EOS.Profiling
{
    /// <summary>Pluggable profiler backend receiving balanced span begin/end calls; implement to bridge to an engine profiler.</summary>
    public interface IEosProfilerBackend
    {
        /// <summary>Begins a named span.</summary>
        void Begin(string label);
        /// <summary>Ends the most recently begun span.</summary>
        void End();
    }
}
