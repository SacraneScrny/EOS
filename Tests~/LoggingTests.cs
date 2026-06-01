using System;

using EOS.Logging;
using Xunit;

namespace EOS.Tests
{
    public sealed class LoggingTests
    {
        [Fact]
        public void LogEntry_FormatsWithSource()
        {
            var entry = new LogEntry(3, LogLevel.Error, "Src", "boom");
            Assert.Equal("[3] Error [Src] boom", entry.ToString());
        }

        [Fact]
        public void LogEntry_FormatsWithoutSource()
        {
            var entry = new LogEntry(2, LogLevel.Warning, null, "careful");
            Assert.Equal("[2] Warning careful", entry.ToString());
        }

        [Fact]
        public void Error_InvokesOnLogWithLevelSourceMessage()
        {
            var previous = EosLog.OnLog;
            try
            {
                LogEntry captured = default;
                EosLog.OnLog = entry => captured = entry;

                EosLog.Error("explode", "UnitSource");

                Assert.Equal(LogLevel.Error, captured.Level);
                Assert.Equal("UnitSource", captured.Source);
                Assert.Equal("explode", captured.Message);
            }
            finally { EosLog.OnLog = previous; }
        }

        [Fact]
        public void Dump_ContainsRecentMessage()
        {
            var previous = EosLog.OnLog;
            try
            {
                EosLog.OnLog = null;
                var unique = "marker-" + Guid.NewGuid();

                EosLog.Warning(unique, "UnitSource");

                Assert.Contains(unique, EosLog.Dump());
            }
            finally { EosLog.OnLog = previous; }
        }
    }
}
