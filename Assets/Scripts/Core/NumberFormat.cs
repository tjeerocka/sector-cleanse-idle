using System;

namespace SectorCleanse.Core
{
    /// <summary>
    /// Compact display for large numbers (soldier tiers go up to 5^100 damage).
    /// 950 → "950", 12 300 → "12.3K", 4.5e6 → "4.5M", ... beyond trillions → "1.23e15".
    /// </summary>
    public static class NumberFormat
    {
        private static readonly string[] Suffixes = { "", "K", "M", "B", "T" };

        public static string Short(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "∞";

            double abs = Math.Abs(value);
            if (abs < 1000d) return Math.Floor(value).ToString("0");

            int group = (int)Math.Floor(Math.Log10(abs) / 3d);
            if (group < Suffixes.Length)
            {
                double scaled = value / Math.Pow(1000d, group);
                return scaled.ToString(scaled < 100d ? "0.#" : "0") + Suffixes[group];
            }

            return value.ToString("0.##e0");
        }
    }
}
