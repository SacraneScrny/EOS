using System;

namespace EOS.Logging
{
    public readonly struct LogEntry
    {
        public readonly int Index;
        public readonly LogLevel Level;
        public readonly string Source;
        public readonly string Message;

        public LogEntry(int index, LogLevel level, string source, string message)
        {
            Index = index;
            Level = level;
            Source = source;
            Message = message;
        }

        public override string ToString()
        {
            return Source != null
                ? $"[{Index}] {Level} [{Source}] {Message}"
                : $"[{Index}] {Level} {Message}";
        }
    }
}