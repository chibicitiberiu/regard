using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Utils;
using Regard.Backend.Common.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Tests.Utils
{
    [TestClass]
    public class JobPruneFilterTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

        private class Row : JobPruneFilter.IPrunableRow
        {
            public long Id { get; set; }
            public JobState State { get; set; }
            public DateTimeOffset? Completed { get; set; }
        }

        private static Row At(long id, JobState state, double daysAgo)
            => new Row { Id = id, State = state, Completed = Now.AddDays(-daysAgo) };

        private static List<long> Prune(IEnumerable<Row> rows, int days = 30, ISet<long> protectedIds = null)
            => JobPruneFilter.SelectPrunable(rows, Now, days, protectedIds).ToList();

        // ---------------------------------------------------------------- the live-row guard

        /// <summary>
        /// The reason this class exists. A recurring job has one row that every fire reuses, and it sits
        /// Completed between fires — so by age and state it is indistinguishable from finished one-shot
        /// work. Deleting it makes the next fire throw "Invalid job ID" and kills that recurring job
        /// until the process restarts.
        /// </summary>
        [TestMethod]
        public void A_row_a_live_trigger_points_at_is_never_pruned_however_old()
        {
            var rows = new[]
            {
                At(1, JobState.Completed, 400),   // ancient recurring row, still live
                At(2, JobState.Completed, 400),   // ancient one-shot
            };

            var doomed = Prune(rows, protectedIds: new HashSet<long> { 1 });

            CollectionAssert.DoesNotContain(doomed, 1L);
            CollectionAssert.Contains(doomed, 2L);
        }

        [TestMethod]
        public void Protecting_nothing_is_allowed()
        {
            Assert.AreEqual(1, Prune(new[] { At(1, JobState.Completed, 400) }, protectedIds: null).Count);
            Assert.AreEqual(1, Prune(new[] { At(1, JobState.Completed, 400) },
                                     protectedIds: new HashSet<long>()).Count);
        }

        // ---------------------------------------------------------------- ages and states

        [TestMethod]
        public void Completed_rows_age_out_on_the_retention_clock()
        {
            var doomed = Prune(new[] { At(1, JobState.Completed, 31), At(2, JobState.Completed, 29) });

            CollectionAssert.Contains(doomed, 1L);
            CollectionAssert.DoesNotContain(doomed, 2L);
        }

        [TestMethod]
        public void Failed_rows_are_kept_three_times_longer_so_problems_stay_visible()
        {
            var doomed = Prune(new[] { At(1, JobState.Failed, 89), At(2, JobState.Failed, 91) });

            CollectionAssert.DoesNotContain(doomed, 1L);
            CollectionAssert.Contains(doomed, 2L);
        }

        /// <summary>
        /// Cancelled rows were not pruned at all before, so they piled up — a fifth of the job table on
        /// the dev install, growing every restart because the boot reconciliation sweep cancels whatever
        /// the last run stranded.
        /// </summary>
        [TestMethod]
        public void Cancelled_rows_are_pruned_too()
        {
            var doomed = Prune(new[] { At(1, JobState.Cancelled, 31), At(2, JobState.Cancelled, 29) });

            CollectionAssert.Contains(doomed, 1L);
            CollectionAssert.DoesNotContain(doomed, 2L);
        }

        [TestMethod]
        [DataRow(JobState.Created)]
        [DataRow(JobState.Scheduled)]
        [DataRow(JobState.Running)]
        public void Non_terminal_rows_are_never_pruned(JobState state)
        {
            // A Running row may be executing right now; a Scheduled one may be a trigger that has not
            // fired yet. Age says nothing useful about either.
            Assert.AreEqual(0, Prune(new[] { At(1, state, 999) }).Count);
        }

        [TestMethod]
        public void A_terminal_row_with_no_completion_time_is_left_alone()
        {
            var row = new Row { Id = 1, State = JobState.Completed, Completed = null };
            Assert.AreEqual(0, Prune(new[] { row }).Count);
        }

        // ---------------------------------------------------------------- the off switch

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void A_non_positive_retention_disables_pruning_rather_than_deleting_everything(int days)
        {
            var rows = new[] { At(1, JobState.Completed, 9999), At(2, JobState.Failed, 9999) };
            Assert.AreEqual(0, Prune(rows, days).Count);
        }

        [TestMethod]
        public void Null_input_is_tolerated()
        {
            Assert.AreEqual(0, JobPruneFilter.SelectPrunable<Row>(null, Now, 30).Count);
        }
    }

    [TestClass]
    public class LogRetentionTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

        private static (string, DateTimeOffset) File(string name, double daysAgo)
            => (name, Now.AddDays(-daysAgo));

        [TestMethod]
        public void Files_older_than_the_retention_go_and_newer_ones_stay()
        {
            var files = new[]
            {
                File("20260820084512PM_417_stdout.txt", 14),
                File("20260901084512PM_418_stdout.txt", 2),
                File("20260903084512AM_419_stdout.txt", 0.1),
            };

            var doomed = LogRetention.SelectForDeletion(files, Now, 7);

            CollectionAssert.AreEquivalent(new[] { "20260820084512PM_417_stdout.txt" }, doomed.ToList());
        }

        [TestMethod]
        public void The_boundary_is_exact()
        {
            var files = new[] { File("a.txt", 7.01), File("b.txt", 6.99) };

            var doomed = LogRetention.SelectForDeletion(files, Now, 7);

            CollectionAssert.AreEquivalent(new[] { "a.txt" }, doomed.ToList());
        }

        /// <summary>
        /// Selection is by mtime, never by the name. These names carry a 12-hour clock with the AM/PM
        /// glued on the end, so 08:45 and 20:45 differ by two characters at position 14 — parsing them
        /// as if they were 24-hour would quietly delete the wrong half of every day.
        /// </summary>
        [TestMethod]
        public void An_old_looking_name_on_a_fresh_file_is_kept()
        {
            var files = new[] { File("20200101120000AM_1_stdout.txt", 0.5) };

            Assert.AreEqual(0, LogRetention.SelectForDeletion(files, Now, 7).Count);
        }

        [TestMethod]
        public void A_fresh_looking_name_on_an_old_file_is_deleted()
        {
            var files = new[] { File("20991231120000PM_1_stdout.txt", 400) };

            Assert.AreEqual(1, LogRetention.SelectForDeletion(files, Now, 7).Count);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-3)]
        public void A_non_positive_retention_keeps_everything(int days)
        {
            var files = new[] { File("a.txt", 9999) };
            Assert.AreEqual(0, LogRetention.SelectForDeletion(files, Now, days).Count);
        }

        [TestMethod]
        public void Null_and_empty_input_are_tolerated()
        {
            Assert.AreEqual(0, LogRetention.SelectForDeletion(null, Now, 7).Count);
            Assert.AreEqual(0, LogRetention.SelectForDeletion(
                Array.Empty<(string, DateTimeOffset)>(), Now, 7).Count);
        }
    }
}
