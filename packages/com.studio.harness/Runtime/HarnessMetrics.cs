using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Harness
{
    /// <summary>
    /// Gameplay reports numbers here (cheap no-op cost outside harness runs is fine — it's a dictionary write).
    ///   HarnessMetrics.Report("tractor.speed", rb.linearVelocity.magnitude);
    ///   HarnessMetrics.Count("crop.cut");
    /// Scenario JSON `expect` blocks assert on name.stat where stat = min|max|avg|last|p95|count.
    /// This is how "it handles well" becomes a number an overnight agent can't argue with.
    /// </summary>
    public static class HarnessMetrics
    {
        const int MaxSamples = 20000;

        class Stat
        {
            public double min = double.MaxValue, max = double.MinValue, sum, last;
            public long n;
            public readonly List<float> samples = new();
        }

        static readonly Dictionary<string, Stat> stats = new();
        public static bool Enabled = true;

        public static void Report(string name, double value)
        {
            if (!Enabled) return;
            if (!stats.TryGetValue(name, out var s)) stats[name] = s = new Stat();
            s.n++; s.sum += value; s.last = value;
            if (value < s.min) s.min = value;
            if (value > s.max) s.max = value;
            if (s.samples.Count < MaxSamples) s.samples.Add((float)value);
        }

        public static void Count(string name, int by = 1)
        {
            if (!stats.TryGetValue(name, out var s)) stats[name] = s = new Stat();
            s.n += by; s.last = s.n; s.sum = s.n; s.min = 0; s.max = s.n;
        }

        public static void Reset() => stats.Clear();

        public static string ToJson()
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in stats)
            {
                var s = kv.Value;
                double p95 = 0;
                if (s.samples.Count > 0)
                {
                    var sorted = s.samples.OrderBy(x => x).ToList();
                    p95 = sorted[(int)System.Math.Min(sorted.Count - 1, System.Math.Floor(sorted.Count * 0.95))];
                }
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key.Replace("\"", "")).Append("\":{")
                  .Append(F("min", s.min)).Append(',').Append(F("max", s.max)).Append(',')
                  .Append(F("avg", s.n > 0 ? s.sum / s.n : 0)).Append(',').Append(F("last", s.last)).Append(',')
                  .Append(F("p95", p95)).Append(',').Append(F("count", s.n)).Append('}');
            }
            return sb.Append('}').ToString();
        }

        static string F(string k, double v) =>
            $"\"{k}\":{(double.IsInfinity(v) || double.IsNaN(v) ? 0 : v).ToString("0.#####", CultureInfo.InvariantCulture)}";
    }
}
