using System;

using EOS.Core;
using EOS.Logging;
using EOS.Serialization;

namespace EOS.Loader
{
    /// <summary>Resets all static state that outlives a <see cref="World"/> (log handler, <see cref="Universe"/>, loader hooks, incarnation binders); call on domain reload and before re-booting.</summary>
    public static class EosDomainReset
    {
        /// <summary>Restores the default log handler, shuts down the <see cref="Universe"/>, and resets the <see cref="WorldLoader"/> and <see cref="IncarnationBridge"/>.</summary>
        public static void Reset()
        {
            EosLog.OnLog = entry => Console.WriteLine(entry.ToString());
            Universe.Shutdown();
            WorldLoader.Reset();
            IncarnationBridge.Reset();
        }
    }
}
