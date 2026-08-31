using System;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// How often a video's metadata is worth re-fetching, as a function of how old the video is.
    ///
    /// View counts, likes and titles move quickly in the days after publication and then almost stop, so
    /// polling every video on one fixed schedule spends most of a very limited budget re-reading numbers
    /// that haven't changed. yt-dlp extractions are paced 5-20 s apart per host and are not covered by the
    /// hour/day caps (those count downloads only), so the only real control is how few videos we ask for.
    ///
    /// Pure and static, like <see cref="PublishDateFilter"/> — no clock of its own, so it's testable.
    /// </summary>
    public static class RefreshSchedule
    {
        /// <summary>
        /// The refresh interval for a video published <paramref name="published"/>, as of
        /// <paramref name="now"/>.
        ///
        /// <code>
        ///   age &lt; 7 days   -> every day
        ///   age &lt; 30 days  -> every 3 days
        ///   age &lt; 90 days  -> every week
        ///   age &lt; 1 year   -> every month
        ///   age &gt;= 1 year  -> every 3 months
        /// </code>
        ///
        /// The curve is self-limiting: a library skewed towards old videos generates little demand, and
        /// the handful of genuinely new videos get the budget — which is the intent.
        /// </summary>
        public static TimeSpan IntervalFor(DateTimeOffset published, DateTimeOffset now)
        {
            var age = now - published;

            if (age < TimeSpan.FromDays(7)) return TimeSpan.FromDays(1);
            if (age < TimeSpan.FromDays(30)) return TimeSpan.FromDays(3);
            if (age < TimeSpan.FromDays(90)) return TimeSpan.FromDays(7);
            if (age < TimeSpan.FromDays(365)) return TimeSpan.FromDays(30);
            return TimeSpan.FromDays(90);
        }

        /// <summary>
        /// True when a video last refreshed at <paramref name="lastUpdated"/> is due again.
        ///
        /// A publish date in the future (clock skew, or a scheduled premiere) yields a negative age and
        /// therefore the shortest interval, which is the right answer: an unreleased video is the one most
        /// likely to change.
        /// </summary>
        public static bool IsDue(DateTimeOffset published, DateTimeOffset lastUpdated, DateTimeOffset now)
        {
            return now - lastUpdated >= IntervalFor(published, now);
        }

        /// <summary>
        /// How overdue a video is, for ordering. Positive means due. Not currently used for ordering —
        /// the job sorts by publish date, newest first — but it makes "how far behind are we?" reportable.
        /// </summary>
        public static TimeSpan Overdue(DateTimeOffset published, DateTimeOffset lastUpdated, DateTimeOffset now)
        {
            return (now - lastUpdated) - IntervalFor(published, now);
        }
    }
}
