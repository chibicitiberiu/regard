using System;
using System.Globalization;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// The per-subscription publish-date window ("only download videos published between X and Y").
    ///
    /// Bounds are held as "yyyy-MM-dd" strings rather than DateTimeOffset?, because that is what an
    /// &lt;input type="date"&gt; produces, what the option store's existing string overloads persist, and
    /// what makes "" mean "inherit / no bound" for free.
    ///
    /// Pure and static, like <see cref="SubscriptionFilterExtensions"/>, so it can run inside the
    /// in-memory half of the download-candidate query.
    /// </summary>
    public static class PublishDateFilter
    {
        private const string BoundFormat = "yyyy-MM-dd";

        /// <summary>
        /// Parses a "yyyy-MM-dd" bound as midnight UTC. Returns false for null, empty, or anything that
        /// isn't exactly that format — callers treat a failure as "no bound", never as "block everything".
        /// </summary>
        public static bool TryParseBound(string value, out DateTimeOffset parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return DateTimeOffset.TryParseExact(
                value.Trim(),
                BoundFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed);
        }

        /// <summary>
        /// True when <paramref name="published"/> falls inside the window.
        ///
        /// Boundaries, because this is where the off-by-one lives: <paramref name="after"/> is inclusive
        /// from midnight UTC of that day, and <paramref name="before"/> is inclusive of the WHOLE of its
        /// day — "2024-12-31" accepts 2024-12-31T23:59:59Z and rejects 2025-01-01T00:00:00Z. A user
        /// typing an end date means "up to and including this day", not "up to midnight as it begins".
        ///
        /// An unset or unparseable bound imposes no restriction. Failing open is deliberate: a typo in a
        /// settings field must not silently stop every download for a subscription.
        /// </summary>
        public static bool PassesDateWindow(DateTimeOffset published, string after, string before)
        {
            if (TryParseBound(after, out var lower) && published < lower)
                return false;

            // AddDays(1) turns the inclusive end day into an exclusive upper bound.
            if (TryParseBound(before, out var upper) && published >= upper.AddDays(1))
                return false;

            return true;
        }

        /// <summary>
        /// True when both bounds are set and the window is inverted (after &gt; before), i.e. a window
        /// that can never match anything. Used for validation on save.
        /// </summary>
        public static bool IsInvertedWindow(string after, string before)
        {
            return TryParseBound(after, out var lower)
                && TryParseBound(before, out var upper)
                && lower > upper;
        }

        /// <summary>
        /// True when the value is non-empty but not a valid "yyyy-MM-dd" date. Empty means "no bound",
        /// which is always valid.
        /// </summary>
        public static bool IsMalformedBound(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && !TryParseBound(value, out _);
        }

        /// <summary>
        /// Validates a pair of bounds for saving, returning a user-facing message or null when the pair
        /// is fine. Shared by the subscription and user-settings endpoints so the two can't drift.
        /// </summary>
        public static string DescribeValidationError(string after, string before)
        {
            if (IsMalformedBound(after))
                return $"\"Published after\" must be a date in yyyy-MM-dd form (got \"{after}\").";

            if (IsMalformedBound(before))
                return $"\"Published before\" must be a date in yyyy-MM-dd form (got \"{before}\").";

            if (IsInvertedWindow(after, before))
                return "\"Published after\" must not be later than \"Published before\" — that window can never match a video.";

            return null;
        }
    }
}
