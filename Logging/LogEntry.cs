using System;

namespace EOS.Logging
{
    /// <summary>A single immutable log record delivered to <see cref="EosLog.OnLog"/> and stored in the ring buffer.</summary>
    public readonly struct LogEntry
    {
        /// <summary>Monotonic sequence number of this entry across the session.</summary>
        public readonly int Index;
        /// <summary>Severity of the entry.</summary>
        public readonly LogLevel Level;
        /// <summary>Originating class name (typically <c>nameof(TheClass)</c>); may be null.</summary>
        public readonly string Source;
        /// <summary>The log text.</summary>
        public readonly string Message;

        /// <summary>Creates a log entry from its sequence index, level, source and message.</summary>
        public LogEntry(int index, LogLevel level, string source, string message)
        {
            Index = index;
            Level = level;
            Source = source;
            Message = message;
        }

        /// <summary>Formats the entry as <c>[index] Level [Source] Message</c>.</summary>
        public override string ToString()
        {
            return Source != null
                ? $"[{Index}] {Level} [{Source}] {Message}"
                : $"[{Index}] {Level} {Message}";
        }
    }
}