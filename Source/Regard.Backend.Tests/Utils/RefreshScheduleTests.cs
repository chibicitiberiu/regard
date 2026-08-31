using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Utils;
using System;

namespace Regard.Backend.Tests.Utils
{
    /// <summary>
    /// The age-based refresh curve. The boundaries are the whole point — a video that has just crossed
    /// into the next band must move to the longer interval, not stay on the shorter one and keep
    /// spending a budget that only allows a handful of extractions an hour.
    /// </summary>
    [TestClass]
    public class RefreshScheduleTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        private static DateTimeOffset AgeDays(double days) => Now - TimeSpan.FromDays(days);

        [TestMethod]
        [DataRow(0.0, 1)]
        [DataRow(6.9, 1)]
        [DataRow(7.0, 3)]     // boundary: leaves the "brand new" band
        [DataRow(29.9, 3)]
        [DataRow(30.0, 7)]
        [DataRow(89.9, 7)]
        [DataRow(90.0, 30)]
        [DataRow(364.9, 30)]
        [DataRow(365.0, 90)]
        [DataRow(4000.0, 90)]
        public void IntervalFor_follows_the_documented_curve(double ageDays, int expectedDays)
        {
            var interval = RefreshSchedule.IntervalFor(AgeDays(ageDays), Now);
            Assert.AreEqual(TimeSpan.FromDays(expectedDays), interval, $"age {ageDays}d");
        }

        /// <summary>
        /// A premiere scheduled for next week has a negative age. The shortest interval is the right
        /// answer there — an unreleased video is the one most likely to change.
        /// </summary>
        [TestMethod]
        public void A_future_publish_date_gets_the_shortest_interval()
        {
            Assert.AreEqual(TimeSpan.FromDays(1), RefreshSchedule.IntervalFor(Now.AddDays(7), Now));
        }

        [TestMethod]
        public void The_interval_never_decreases_as_a_video_ages()
        {
            TimeSpan previous = TimeSpan.Zero;
            for (double d = 0; d < 800; d += 0.5)
            {
                var interval = RefreshSchedule.IntervalFor(AgeDays(d), Now);
                Assert.IsTrue(interval >= previous, $"interval shrank at age {d}d");
                previous = interval;
            }
        }

        // --- IsDue ----------------------------------------------------------------------------------

        [TestMethod]
        public void A_new_video_is_due_after_a_day()
        {
            var published = AgeDays(2);
            Assert.IsFalse(RefreshSchedule.IsDue(published, Now.AddHours(-23), Now));
            Assert.IsTrue(RefreshSchedule.IsDue(published, Now.AddHours(-25), Now));
        }

        [TestMethod]
        public void An_old_video_is_not_due_after_a_day()
        {
            var published = AgeDays(1000);
            Assert.IsFalse(RefreshSchedule.IsDue(published, Now.AddDays(-1), Now));
            Assert.IsFalse(RefreshSchedule.IsDue(published, Now.AddDays(-89), Now));
            Assert.IsTrue(RefreshSchedule.IsDue(published, Now.AddDays(-91), Now));
        }

        [TestMethod]
        public void A_video_refreshed_just_now_is_never_due()
        {
            foreach (var age in new[] { 1.0, 20.0, 200.0, 2000.0 })
                Assert.IsFalse(RefreshSchedule.IsDue(AgeDays(age), Now, Now), $"age {age}d");
        }

        /// <summary>
        /// MinValue is the placeholder sync stamps on un-enriched videos. It reads as ancient, so it lands
        /// on the longest interval — which is exactly backwards for something never fetched at all, and is
        /// why the refresh job excludes un-enriched videos rather than relying on this.
        /// </summary>
        [TestMethod]
        public void MinValue_publish_date_lands_on_the_longest_interval()
        {
            Assert.AreEqual(TimeSpan.FromDays(90), RefreshSchedule.IntervalFor(DateTimeOffset.MinValue, Now));
        }

        [TestMethod]
        public void Overdue_is_positive_exactly_when_due()
        {
            var published = AgeDays(2);      // 1-day interval
            Assert.IsTrue(RefreshSchedule.Overdue(published, Now.AddDays(-3), Now) > TimeSpan.Zero);
            Assert.IsTrue(RefreshSchedule.Overdue(published, Now.AddHours(-1), Now) < TimeSpan.Zero);
        }
    }
}
