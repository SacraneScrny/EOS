namespace EOS.Profiling
{
    public interface IEosProfilerBackend
    {
        void Begin(string label);
        void End();
    }
}
