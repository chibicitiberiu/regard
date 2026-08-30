using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Utils;
using System;
using System.Globalization;

namespace Regard.Backend.Tests.Utils
{
    [TestClass]
    public class PublishDateFilterTests
    {
        private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(
            iso, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        // --- bounds --------------------------------------------------------------------------------

        [TestMethod]
        public void TryParseBound_reads_an_iso_date_as_midnight_utc()
        {
            Assert.IsTrue(PublishDateFilter.TryParseBound("2024-03-05", out var parsed));
            Assert.AreEqual(new DateTimeOffset(2024, 3, 5, 0, 0, 0, TimeSpan.Zero), parsed);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("2024-3-5")]          // not zero-padded
        [DataRow("05/03/2024")]        // a locale format we deliberately don't accept
        [DataRow("2024-03-05T10:00")]  // date only, no time component
        [DataRow("yesterday")]
        [DataRow("2024-13-01")]        // month out of range
        public void TryParseBound_rejects_anything_but_yyyy_MM_dd(string value)
        {
            Assert.IsFalse(PublishDateFilter.TryParseBound(value, out _));
        }

        [TestMethod]
        public void TryParseBound_tolerates_surrounding_whitespace()
        {
            Assert.IsTrue(PublishDateFilter.TryParseBound("  2024-03-05  ", out _));
        }

        // --- the window ----------------------------------------------------------------------------

        [TestMethod]
        public void No_bounds_lets_everything_through()
        {
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("1999-01-01T00:00:00Z"), null, null));
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2050-01-01T00:00:00Z"), "", ""));
        }

        [TestMethod]
        public void Lower_bound_is_inclusive_from_midnight()
        {
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-01-01T00:00:00Z"), "2024-01-01", null));
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-01-01T00:00:01Z"), "2024-01-01", null));
            Assert.IsFalse(PublishDateFilter.PassesDateWindow(Utc("2023-12-31T23:59:59Z"), "2024-01-01", null));
        }

        /// <summary>
        /// The off-by-one this whole design turns on: an end date means "up to and including this day",
        /// not "up to the instant this day begins". Get it wrong and a window ending today silently
        /// excludes everything published today.
        /// </summary>
        [TestMethod]
        public void Upper_bound_includes_the_whole_of_its_day()
        {
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-12-31T00:00:00Z"), null, "2024-12-31"));
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-12-31T12:34:56Z"), null, "2024-12-31"));
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-12-31T23:59:59Z"), null, "2024-12-31"));
            Assert.IsFalse(PublishDateFilter.PassesDateWindow(Utc("2025-01-01T00:00:00Z"), null, "2024-12-31"));
        }

        [TestMethod]
        public void Both_bounds_form_a_closed_window()
        {
            const string after = "2024-01-01", before = "2024-12-31";
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-06-15T10:00:00Z"), after, before));
            Assert.IsFalse(PublishDateFilter.PassesDateWindow(Utc("2023-12-31T10:00:00Z"), after, before));
            Assert.IsFalse(PublishDateFilter.PassesDateWindow(Utc("2025-01-01T10:00:00Z"), after, before));
        }

        [TestMethod]
        public void A_single_day_window_accepts_exactly_that_day()
        {
            const string day = "2024-06-15";
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-06-15T00:00:00Z"), day, day));
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-06-15T23:59:59Z"), day, day));
            Assert.IsFalse(PublishDateFilter.PassesDateWindow(Utc("2024-06-14T23:59:59Z"), day, day));
            Assert.IsFalse(PublishDateFilter.PassesDateWindow(Utc("2024-06-16T00:00:00Z"), day, day));
        }

        [TestMethod]
        public void A_published_date_in_another_offset_is_compared_as_an_instant()
        {
            // 2024-01-01T00:30+02:00 is 2023-12-31T22:30Z, so it falls BEFORE a 2024-01-01 lower bound.
            var published = new DateTimeOffset(2024, 1, 1, 0, 30, 0, TimeSpan.FromHours(2));
            Assert.IsFalse(PublishDateFilter.PassesDateWindow(published, "2024-01-01", null));
        }

        /// <summary>
        /// Failing open is deliberate: a typo in a settings field must not silently stop every download
        /// for a subscription. The UI and the API both reject malformed input on save, so this only
        /// covers a value that somehow reached storage anyway.
        /// </summary>
        [TestMethod]
        [DataRow("not-a-date", null)]
        [DataRow(null, "not-a-date")]
        [DataRow("garbage", "garbage")]
        public void A_malformed_bound_imposes_no_restriction(string after, string before)
        {
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(Utc("2024-06-15T10:00:00Z"), after, before));
        }

        /// <summary>
        /// MinValue is the placeholder sync stamps on un-enriched videos. It is genuinely outside any
        /// lower-bounded window, which is exactly why the callers exempt un-enriched videos rather than
        /// relying on this predicate to be lenient about it.
        /// </summary>
        [TestMethod]
        public void MinValue_fails_a_lower_bound()
        {
            Assert.IsFalse(PublishDateFilter.PassesDateWindow(DateTimeOffset.MinValue, "2024-01-01", null));
            Assert.IsTrue(PublishDateFilter.PassesDateWindow(DateTimeOffset.MinValue, null, null));
        }

        // --- validation ----------------------------------------------------------------------------

        [TestMethod]
        public void Validation_accepts_an_empty_or_ordered_window()
        {
            Assert.IsNull(PublishDateFilter.DescribeValidationError(null, null));
            Assert.IsNull(PublishDateFilter.DescribeValidationError("", ""));
            Assert.IsNull(PublishDateFilter.DescribeValidationError("2024-01-01", "2024-12-31"));
            Assert.IsNull(PublishDateFilter.DescribeValidationError("2024-01-01", "2024-01-01"));
            Assert.IsNull(PublishDateFilter.DescribeValidationError("2024-01-01", null));
        }

        [TestMethod]
        public void Validation_rejects_an_inverted_window()
        {
            var error = PublishDateFilter.DescribeValidationError("2024-12-31", "2024-01-01");
            Assert.IsNotNull(error);
            StringAssert.Contains(error, "must not be later");
        }

        [TestMethod]
        public void Validation_rejects_a_malformed_bound_and_names_which_one()
        {
            StringAssert.Contains(PublishDateFilter.DescribeValidationError("nonsense", "2024-01-01"), "Published after");
            StringAssert.Contains(PublishDateFilter.DescribeValidationError("2024-01-01", "nonsense"), "Published before");
        }

        [TestMethod]
        public void IsInvertedWindow_needs_both_bounds()
        {
            Assert.IsFalse(PublishDateFilter.IsInvertedWindow("2024-12-31", null));
            Assert.IsFalse(PublishDateFilter.IsInvertedWindow(null, "2024-01-01"));
            Assert.IsTrue(PublishDateFilter.IsInvertedWindow("2024-12-31", "2024-01-01"));
        }
    }
}
