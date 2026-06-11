using System;

using EOS.Core;
using EOS.Logging;
using EOS.Serialization;

namespace EOS.Loader
{
    public static class EosDomainReset
    {
        public static void Reset()
        {
            EosLog.OnLog = entry => Console.WriteLine(entry.ToString());
            Universe.Shutdown();
            WorldLoader.Reset();
            IncarnationBridge.Reset();
        }
    }
}
