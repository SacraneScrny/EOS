using System;
using System.Diagnostics;
using System.Text;

namespace EOS.Logging
{
    /// <summary>Static logging facade backed by a 1024-entry ring buffer; route output by replacing <see cref="OnLog"/>. Always pass <c>nameof(TheClass)</c> as the source.</summary>
    public static class EosLog
    {
        const int BufferSize = 1024;
        static readonly LogEntry[] Buffer = new LogEntry[BufferSize];
        static int _head;
        static int _totalCount;

        /// <summary>Handler invoked for every log entry; defaults to writing to the console. The Unity bridge replaces it.</summary>
        public static Action<LogEntry> OnLog = entry => Console.WriteLine(entry.ToString());

        /// <summary>Logs a debug message; compiled out in non-DEBUG builds.</summary>
        [Conditional("DEBUG")]
        public static void Debug(string message, string source = null) =>
            Write(LogLevel.Debug, message, source);

        /// <summary>Logs a warning message.</summary>
        public static void Warning(string message, string source = null) =>
            Write(LogLevel.Warning, message, source);

        /// <summary>Logs an error message.</summary>
        public static void Error(string message, string source = null) =>
            Write(LogLevel.Error, message, source);

        /// <summary>Returns the buffered entries oldest-first as a newline-joined string.</summary>
        public static string Dump()
        {
            int count = Math.Min(_totalCount, BufferSize);
            if (count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int start = _totalCount <= BufferSize ? 0 : _head;

            for (int i = 0; i < count; i++)
            {
                int index = (start + i) % BufferSize;
                sb.AppendLine(Buffer[index].ToString());
            }

            return sb.ToString();
        }
        static void Write(LogLevel level, string message, string source)
        {
            var entry = new LogEntry(_totalCount, level, source, message);
            Buffer[_head] = entry;
            _head = (_head + 1) % BufferSize;
            _totalCount++;
            OnLog?.Invoke(entry);
        }
    }
}