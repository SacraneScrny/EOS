using System;
using System.Diagnostics;
using System.Text;

namespace EOS.Logging
{
    public static class EosLog
    {
        private const int BufferSize = 128;

        private static readonly LogEntry[] Buffer = new LogEntry[BufferSize];
        private static int _head;
        private static int _totalCount;

        public static Action<LogEntry> OnLog = entry => Console.WriteLine(entry.ToString());

        [Conditional("DEBUG")]
        public static void Debug(string message, string source = null) =>
            Write(LogLevel.Debug, message, source);

        public static void Warning(string message, string source = null) =>
            Write(LogLevel.Warning, message, source);

        public static void Error(string message, string source = null) =>
            Write(LogLevel.Error, message, source);

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

        private static void Write(LogLevel level, string message, string source)
        {
            var entry = new LogEntry(_totalCount, level, source, message);
            Buffer[_head] = entry;
            _head = (_head + 1) % BufferSize;
            _totalCount++;
            OnLog?.Invoke(entry);
        }
    }
}