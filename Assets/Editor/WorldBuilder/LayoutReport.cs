using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One row of the Validate Layout table: what was checked, on what, the measured value and the limit.</summary>
    public readonly struct LayoutCheck
    {
        public readonly string Check;
        public readonly string Subject;
        public readonly string Measured;
        public readonly string Limit;
        public readonly bool Passed;

        public LayoutCheck(string check, string subject, string measured, string limit, bool passed)
        {
            Check = check;
            Subject = subject;
            Measured = measured;
            Limit = limit;
            Passed = passed;
        }
    }

    /// <summary>Turns layout checks into the pass/fail table that Validate Layout prints to the Console.</summary>
    public static class LayoutReport
    {
        private static readonly string[] Headers = { "Check", "Subject", "Measured", "Limit", "Result" };

        public static bool AllPassed(IReadOnlyList<LayoutCheck> checks)
        {
            return checks.Count > 0 && Failures(checks) == 0;
        }

        public static int Failures(IReadOnlyList<LayoutCheck> checks)
        {
            int failures = 0;
            foreach (LayoutCheck check in checks)
            {
                if (!check.Passed)
                {
                    failures++;
                }
            }

            return failures;
        }

        /// <summary>A one-line summary followed by the table, with columns padded to line up in a monospaced log.</summary>
        public static string Format(IReadOnlyList<LayoutCheck> checks)
        {
            var rows = new List<string[]> { Headers };
            foreach (LayoutCheck check in checks)
            {
                rows.Add(new[] { check.Check, check.Subject, check.Measured, check.Limit, check.Passed ? "PASS" : "FAIL" });
            }

            var widths = new int[Headers.Length];
            foreach (string[] row in rows)
            {
                for (int column = 0; column < row.Length; column++)
                {
                    widths[column] = Mathf.Max(widths[column], row[column].Length);
                }
            }

            int failures = Failures(checks);
            var text = new StringBuilder();
            text.Append("[World] Validate Layout: ").Append(AllPassed(checks) ? "PASS" : "FAIL")
                .Append($", {checks.Count - failures} of {checks.Count} checks passed.").AppendLine();
            for (int i = 0; i < rows.Count; i++)
            {
                AppendRow(text, rows[i], widths);
                if (i == 0)
                {
                    AppendRule(text, widths);
                }
            }

            return text.ToString();
        }

        /// <summary>Length of a path along its corners, in metres.</summary>
        public static float PathLength(Vector3[] corners)
        {
            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        private static void AppendRow(StringBuilder text, string[] cells, int[] widths)
        {
            for (int column = 0; column < cells.Length; column++)
            {
                text.Append(column == 0 ? string.Empty : " | ").Append(cells[column].PadRight(widths[column]));
            }

            text.AppendLine();
        }

        private static void AppendRule(StringBuilder text, int[] widths)
        {
            for (int column = 0; column < widths.Length; column++)
            {
                text.Append(column == 0 ? string.Empty : "-+-").Append('-', widths[column]);
            }

            text.AppendLine();
        }
    }
}
