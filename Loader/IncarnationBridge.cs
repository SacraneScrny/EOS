namespace EOS.Loader
{
    public static class IncarnationBridge
    {
        public static IIncarnationBinder Binder { get; set; }

        public static void Reset() => Binder = null;
    }
}
