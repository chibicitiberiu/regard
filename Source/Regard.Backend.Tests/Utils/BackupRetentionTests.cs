using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Tests.Utils
{
    /// <summary>
    /// Retention decides what gets deleted, so these tests are weighted towards what must NOT happen
    /// rather than towards the happy path. The backup directory lives under the data directory, where
    /// hand-made copies are the expected case, and this code runs unattended at 3am.
    /// </summary>
    [TestClass]
    public class BackupRetentionTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

        private static (string, DateTimeOffset) At(int daysAgo, bool preMigration = false)
        {
            var stamp = Now.AddDays(-daysAgo);
            return (BackupRetention.NameFor(stamp, preMigration), stamp);
        }

        private static List<(string, DateTimeOffset)> Series(int count, bool preMigration = false)
            => Enumerable.Range(1, count).Select(d => At(d, preMigration)).ToList();

        // ---------------------------------------------------------------- naming round-trip

        [TestMethod]
        public void NameFor_round_trips_through_TryParseName()
        {
            var name = BackupRetention.NameFor(Now);

            Assert.IsTrue(BackupRetention.TryParseName(name, out var parsed, out bool pre));
            Assert.AreEqual(Now.ToUnixTimeSeconds(), parsed.ToUnixTimeSeconds());
            Assert.IsFalse(pre);
        }

        [TestMethod]
        public void NameFor_uses_UTC_so_a_DST_fallback_cannot_produce_two_identical_names()
        {
            // 2026-10-25 01:30 UTC is 03:30 and then again 02:30 in a CET/CEST fall-back. Two distinct
            // instants an hour apart must not collapse to the same file name, because retention orders
            // by that name and "delete the oldest" would otherwise pick arbitrarily.
            var first = new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2));
            var second = new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(1));

            Assert.AreNotEqual(BackupRetention.NameFor(first), BackupRetention.NameFor(second));
        }

        [TestMethod]
        public void PreMigration_names_round_trip_and_are_flagged()
        {
            var name = BackupRetention.NameFor(Now, preMigration: true);

            StringAssert.Contains(name, "premigration");
            Assert.IsTrue(BackupRetention.TryParseName(name, out _, out bool pre));
            Assert.IsTrue(pre);
        }

        // ---------------------------------------------------------------- what is NOT a candidate

        [TestMethod]
        [DataRow("Regard.db")]                                  // the live database itself
        [DataRow("Regard.db-wal")]
        [DataRow("Regard.db-shm")]
        [DataRow("Regard.db.20260830-220853-pre-batch5a.bak")]  // a real hand-made copy from this repo
        [DataRow("Regard-20260903T120000Z.db.tmp")]             // half-written, still being verified
        [DataRow("Regard-notatimestamp.db")]
        [DataRow("Regard-20260903T120000Z.sqlite")]             // right stamp, wrong extension
        [DataRow("regard-20260903T120000Z.db")]                 // wrong case on the prefix
        [DataRow("jwt-secret")]
        [DataRow("")]
        public void TryParseName_rejects_anything_this_class_did_not_write(string name)
        {
            Assert.IsFalse(BackupRetention.TryParseName(name, out _, out _),
                $"'{name}' must not be treated as a backup");
        }

        [TestMethod]
        public void SelectForDeletion_never_returns_a_file_it_does_not_recognise()
        {
            var files = Series(20);
            files.Add(("Regard.db", Now));
            files.Add(("jwt-secret", Now));
            files.Add(("Regard.db.20260830-220853-pre-batch5a.bak", Now.AddDays(-30)));
            files.Add(("Regard-20260101T000000Z.db.tmp", Now.AddDays(-200)));

            var doomed = BackupRetention.SelectForDeletion(files, keepCount: 7);

            foreach (var stranger in new[] { "Regard.db", "jwt-secret",
                                             "Regard.db.20260830-220853-pre-batch5a.bak",
                                             "Regard-20260101T000000Z.db.tmp" })
                CollectionAssert.DoesNotContain(doomed.ToList(), stranger);
        }

        // ---------------------------------------------------------------- the retention itself

        [TestMethod]
        public void SelectForDeletion_keeps_the_newest_N()
        {
            var files = Series(20);   // 1..20 days ago

            var doomed = BackupRetention.SelectForDeletion(files, keepCount: 7).ToHashSet();
            var survivors = files.Select(f => f.Item1).Where(n => !doomed.Contains(n)).ToList();

            Assert.AreEqual(7, survivors.Count);
            // the survivors are exactly days 1..7 ago
            foreach (var day in Enumerable.Range(1, 7))
                CollectionAssert.Contains(survivors, At(day).Item1);
        }

        [TestMethod]
        public void SelectForDeletion_does_nothing_when_under_the_limit()
        {
            Assert.AreEqual(0, BackupRetention.SelectForDeletion(Series(3), keepCount: 7).Count);
        }

        [TestMethod]
        public void SelectForDeletion_treats_a_non_positive_keep_count_as_keep_everything()
        {
            // The dangerous reading of 0 is not the useful one. An admin typing 0 into a box has not
            // asked to have every snapshot erased.
            Assert.AreEqual(0, BackupRetention.SelectForDeletion(Series(20), keepCount: 0).Count);
            Assert.AreEqual(0, BackupRetention.SelectForDeletion(Series(20), keepCount: -5).Count);
        }

        [TestMethod]
        public void SelectForDeletion_always_leaves_at_least_one_survivor()
        {
            for (int total = 1; total <= 12; total++)
            {
                var files = Series(total);
                var doomed = BackupRetention.SelectForDeletion(files, keepCount: 1);
                Assert.IsTrue(files.Count - doomed.Count >= 1,
                    $"with {total} files and keepCount 1, nothing survived");
            }
        }

        [TestMethod]
        public void PreMigration_snapshots_are_on_their_own_budget()
        {
            // 10 ordinary + 5 pre-migration. Keeping 7 ordinary must not consume the pre-migration
            // allowance, and vice versa — the whole point of the pre-migration snapshot is that it
            // survives the routine churn that follows an upgrade.
            var files = Series(10).Concat(Series(5, preMigration: true)).ToList();

            var doomed = BackupRetention.SelectForDeletion(files, keepCount: 7, keepPreMigration: 3).ToHashSet();
            var survivors = files.Select(f => f.Item1).Where(n => !doomed.Contains(n)).ToList();

            Assert.AreEqual(7, survivors.Count(n => !n.Contains("premigration")));
            Assert.AreEqual(3, survivors.Count(n => n.Contains("premigration")));
        }

        [TestMethod]
        public void A_flood_of_ordinary_backups_cannot_evict_a_premigration_snapshot()
        {
            var files = Series(200).Concat(Series(1, preMigration: true)).ToList();

            var doomed = BackupRetention.SelectForDeletion(files, keepCount: 7).ToHashSet();

            Assert.IsFalse(doomed.Contains(At(1, preMigration: true).Item1));
        }

        [TestMethod]
        public void SelectForDeletion_tolerates_null_and_empty_input()
        {
            Assert.AreEqual(0, BackupRetention.SelectForDeletion(null, 7).Count);
            Assert.AreEqual(0, BackupRetention.SelectForDeletion(
                Array.Empty<(string, DateTimeOffset)>(), 7).Count);
        }

        // ---------------------------------------------------------------- temp-file debris

        [TestMethod]
        public void Stale_temp_files_are_swept_but_fresh_ones_are_left_alone()
        {
            var files = new[]
            {
                ("Regard-20260903T110000Z.db.tmp", Now.AddMinutes(-2)),    // a backup in flight
                ("Regard-20260902T110000Z.db.tmp", Now.AddHours(-5)),      // debris
                ("Regard-20260901T110000Z.db",     Now.AddDays(-2)),       // a real backup
                ("Regard.db",                      Now),
            };

            var stale = BackupRetention.SelectStaleTempFiles(files, Now, TimeSpan.FromHours(1));

            CollectionAssert.AreEquivalent(new[] { "Regard-20260902T110000Z.db.tmp" }, stale.ToList());
        }

        [TestMethod]
        public void The_temp_sweep_never_touches_a_real_backup_or_the_live_database()
        {
            var files = Series(5).Select(f => (f.Item1, Now.AddDays(-99))).ToList();
            files.Add(("Regard.db", Now.AddDays(-99)));

            Assert.AreEqual(0, BackupRetention.SelectStaleTempFiles(files, Now, TimeSpan.FromHours(1)).Count);
        }
    }
}
