using Regard.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Regard.Backend.Common.Utils
{
    public static class SubscriptionFilterExtensions
    {
        // Mandatory: these run on Quartz scheduler threads against every candidate title,
        // so a catastrophic-backtracking (ReDoS) pattern must not hang a worker.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Compiles filter patterns once (case-insensitive, with the mandatory match timeout).
        /// Invalid patterns are skipped (Edit validates on save; Preview tolerates mid-typing).
        /// </summary>
        public static IReadOnlyList<(FilterAction Action, Regex Regex)> CompileFilters(
            IEnumerable<(FilterAction Action, string Pattern)> filters)
        {
            var result = new List<(FilterAction, Regex)>();
            if (filters == null)
                return result;

            foreach (var (action, pattern) in filters)
            {
                if (string.IsNullOrEmpty(pattern))
                    continue;
                try
                {
                    result.Add((action, new Regex(pattern, RegexOptions.IgnoreCase, MatchTimeout)));
                }
                catch (ArgumentException)
                {
                    // Invalid regex - skip it.
                }
            }
            return result;
        }

        /// <summary>
        /// AND semantics: passes iff every Include matches the title and no Exclude matches.
        /// An empty filter list passes. A match timeout is treated as "did not match".
        /// </summary>
        public static bool PassesTitleFilters(string title, IReadOnlyList<(FilterAction Action, Regex Regex)> compiled)
        {
            title ??= string.Empty;
            foreach (var (action, regex) in compiled)
            {
                bool matched;
                try { matched = regex.IsMatch(title); }
                catch (RegexMatchTimeoutException) { matched = false; }

                if (action == FilterAction.Include && !matched)
                    return false;
                if (action == FilterAction.Exclude && matched)
                    return false;
            }
            return true;
        }
    }
}
