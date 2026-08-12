using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SimpleHitMarker
{
    /// <summary>
    /// Lightweight frame-scoped profiler for tracking down stalls at runtime.
    ///
    /// Usage: wrap a suspect section in <c>using (PerfProbe.Measure("Name")) { ... }</c>.
    /// Timings accumulate per frame; when a frame exceeds the configured spike threshold,
    /// the whole breakdown for that frame is dumped to the log. Quiet frames cost nothing
    /// but a dictionary update and print nothing, so the log only contains actual stalls.
    ///
    /// Disabled by default — turn on "启用性能分析" in the BepInEx config.
    /// </summary>
    internal static class PerfProbe
    {
        /// <summary>Master switch, driven from config each frame in Plugin.Update.</summary>
        public static bool Enabled;

        /// <summary>Frames slower than this (ms) get their breakdown logged.</summary>
        public static float SpikeThresholdMs = 20f;

        private struct Stat
        {
            public double Ms;
            public int Count;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Stat> FrameStats = new Dictionary<string, Stat>(StringComparer.Ordinal);
        private static readonly StringBuilder ReportBuilder = new StringBuilder(256);
        private static readonly List<KeyValuePair<string, Stat>> SortBuffer = new List<KeyValuePair<string, Stat>>(16);

        /// <summary>
        /// Disposable timing scope. A struct, so `using` calls Dispose without boxing and a
        /// disabled probe allocates nothing (default(Scope) has a null name and no-ops).
        /// </summary>
        public readonly struct Scope : IDisposable
        {
            private readonly string _name;
            private readonly long _startTicks;

            internal Scope(string name)
            {
                _name = name;
                _startTicks = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (_name == null) return;
                double ms = (Stopwatch.GetTimestamp() - _startTicks) * 1000.0 / Stopwatch.Frequency;
                Record(_name, ms);
            }
        }

        public static Scope Measure(string name)
        {
            return Enabled ? new Scope(name) : default;
        }

        /// <summary>
        /// Record an occurrence with no duration. The report's "xN" suffix then acts as a plain
        /// per-frame counter — useful for things like "how many Repaint events did we get?".
        /// </summary>
        public static void Count(string name)
        {
            if (!Enabled) return;
            Record(name, 0.0);
        }

        private static void Record(string name, double ms)
        {
            // Damage events can arrive off the main thread, so guard the accumulator.
            lock (Gate)
            {
                FrameStats.TryGetValue(name, out Stat stat);
                stat.Ms += ms;
                stat.Count++;
                FrameStats[name] = stat;
            }
        }

        /// <summary>
        /// Call once per frame from Plugin.Update with the duration of the frame that just
        /// elapsed. Dumps and clears the accumulated breakdown when that frame was a spike.
        /// </summary>
        public static void FrameTick(float frameMs)
        {
            if (!Enabled)
            {
                if (FrameStats.Count > 0)
                {
                    lock (Gate) FrameStats.Clear();
                }
                return;
            }

            lock (Gate)
            {
                if (FrameStats.Count > 0 && frameMs >= SpikeThresholdMs)
                {
                    BuildAndLogReport(frameMs);
                }

                FrameStats.Clear();
            }
        }

        private static void BuildAndLogReport(float frameMs)
        {
            SortBuffer.Clear();
            double ourTotal = 0.0;
            foreach (var kv in FrameStats)
            {
                SortBuffer.Add(kv);
                ourTotal += kv.Value.Ms;
            }

            // Most expensive first — the top line is almost always the culprit.
            SortBuffer.Sort((a, b) => b.Value.Ms.CompareTo(a.Value.Ms));

            ReportBuilder.Length = 0;
            ReportBuilder.Append("[SimpleHitMarker][PERF] frame ").Append(frameMs.ToString("0.0"))
                         .Append("ms, ours ").Append(ourTotal.ToString("0.0")).Append("ms :");

            for (int i = 0; i < SortBuffer.Count; i++)
            {
                var kv = SortBuffer[i];
                ReportBuilder.Append(' ').Append(kv.Key).Append('=')
                             .Append(kv.Value.Ms.ToString("0.00")).Append("ms");
                if (kv.Value.Count > 1)
                {
                    ReportBuilder.Append('x').Append(kv.Value.Count);
                }
                ReportBuilder.Append(';');
            }

            Plugin.Log?.LogWarning(ReportBuilder.ToString());
        }
    }
}
