namespace EOS.Logging
{
    /// <summary>Severity of a <see cref="LogEntry"/>, also used as the minimum-level filter on log handlers.</summary>
    public enum LogLevel
    {
        /// <summary>Diagnostic detail; emitted only in DEBUG builds.</summary>
        Debug,
        /// <summary>A recoverable problem that doesn't stop execution.</summary>
        Warning,
        /// <summary>A failure or invalid operation.</summary>
        Error
    }
}