using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace EOS.Profiling
{
    public sealed class AggregatedProfilerBackend : IEosProfilerBackend
    {
        readonly struct Frame
        {
            public readonly string Label;
            public readonly long Start;

            public Frame(string label, long start)
            {
                Label = label;
                Start = start;
            }
        }

        readonly Stack<Frame> _stack = new();
        readonly Dictionary<string, long> _ticks = new();
        readonly Dictionary<string, int> _calls = new();

        public void Begin(string label) => _stack.Push(new Frame(label, Stopwatch.GetTimestamp()));

        public void End()
        {
            if (_stack.Count == 0) return;
            var frame = _stack.Pop();
            long elapsed = Stopwatch.GetTimestamp() - frame.Start;
            _ticks.TryGetValue(frame.Label, out var total);
            _ticks[frame.Label] = total + elapsed;
            _calls.TryGetValue(frame.Label, out var count);
            _calls[frame.Label] = count + 1;
        }

        public void Reset()
        {
            _stack.Clear();
            _ticks.Clear();
            _calls.Clear();
        }

        public string Dump(bool reset = true)
        {
            if (_ticks.Count == 0)
                return string.Empty;

            double toMs = 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder();
            foreach (var pair in _ticks.OrderByDescending(p => p.Value))
            {
                int calls = _calls.TryGetValue(pair.Key, out var c) ? c : 0;
                double ms = pair.Value * toMs;
                double avg = calls > 0 ? ms / calls : 0.0;
                sb.AppendLine($"{pair.Key}: {ms:F3}ms over {calls} calls ({avg:F3}ms avg)");
            }

            if (reset) Reset();
            return sb.ToString();
        }
    }
}
