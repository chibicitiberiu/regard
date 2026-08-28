using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Jellyfin;
using System;

namespace Regard.Backend.Tests.Jellyfin
{
    [TestClass]
    public class JellyfinReconcilerTests
    {
        private const long TicksPerSec = TimeSpan.TicksPerSecond; // 10,000,000

        private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateTime T0Utc = T0.UtcDateTime;
        private static readonly DateTime Newer = T0Utc.AddHours(1);
        private static readonly DateTime Older = T0Utc.AddHours(-1);

        // --- Watched priority ---------------------------------------------------------------

        [TestMethod]
        public void JellyfinPlayed_RegardNotWatched_MarksWatched()
        {
            var d = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: 40, regardUpdated: T0,
                jellyfinPlayed: true, jellyfinPositionTicks: 0, jellyfinLastPlayed: Newer);
            Assert.AreEqual(JellyfinSyncAction.MarkWatched, d.Action);
        }

        [TestMethod]
        public void JellyfinPlayed_RegardAlreadyWatched_NoOp()
        {
            var d = JellyfinReconciler.Reconcile(
                regardWatched: true, regardPositionSeconds: null, regardUpdated: null,
                jellyfinPlayed: true, jellyfinPositionTicks: null, jellyfinLastPlayed: Newer);
            Assert.AreEqual(JellyfinSyncAction.None, d.Action);
        }

        [TestMethod]
        public void RegardWatched_JellyfinNotPlayed_PushesPlayed()
        {
            var d = JellyfinReconciler.Reconcile(
                regardWatched: true, regardPositionSeconds: null, regardUpdated: T0,
                jellyfinPlayed: false, jellyfinPositionTicks: 30 * TicksPerSec, jellyfinLastPlayed: Older);
            Assert.AreEqual(JellyfinSyncAction.PushToJellyfin, d.Action);
            Assert.IsTrue(d.PushPlayed);
            Assert.AreEqual(0L, d.PushTicks);
        }

        // --- Position newer-wins ------------------------------------------------------------

        [TestMethod]
        public void JellyfinPositionNewer_Adopts_WithJellyfinTimestamp()
        {
            var d = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: 20, regardUpdated: T0,
                jellyfinPlayed: false, jellyfinPositionTicks: 90 * TicksPerSec, jellyfinLastPlayed: Newer);
            Assert.AreEqual(JellyfinSyncAction.AdoptPosition, d.Action);
            Assert.AreEqual(90, d.PositionSeconds);
            // Adopt must carry Jellyfin's timestamp, not "now", to avoid ping-pong.
            Assert.AreEqual(new DateTimeOffset(Newer, TimeSpan.Zero), d.Timestamp);
        }

        [TestMethod]
        public void RegardPositionNewer_PushesPosition_NotPlayed()
        {
            var d = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: 120, regardUpdated: T0,
                jellyfinPlayed: false, jellyfinPositionTicks: 30 * TicksPerSec, jellyfinLastPlayed: Older);
            Assert.AreEqual(JellyfinSyncAction.PushToJellyfin, d.Action);
            Assert.IsFalse(d.PushPlayed);
            Assert.AreEqual(120L * TicksPerSec, d.PushTicks);
        }

        [TestMethod]
        public void OnlyJellyfinHasPosition_Adopts()
        {
            var d = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: null, regardUpdated: null,
                jellyfinPlayed: false, jellyfinPositionTicks: 45 * TicksPerSec, jellyfinLastPlayed: Older);
            Assert.AreEqual(JellyfinSyncAction.AdoptPosition, d.Action);
            Assert.AreEqual(45, d.PositionSeconds);
        }

        [TestMethod]
        public void OnlyRegardHasPosition_Pushes()
        {
            var d = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: 75, regardUpdated: T0,
                jellyfinPlayed: false, jellyfinPositionTicks: null, jellyfinLastPlayed: null);
            Assert.AreEqual(JellyfinSyncAction.PushToJellyfin, d.Action);
            Assert.AreEqual(75L * TicksPerSec, d.PushTicks);
        }

        [TestMethod]
        public void NeitherHasPosition_NoOp()
        {
            var d = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: null, regardUpdated: null,
                jellyfinPlayed: false, jellyfinPositionTicks: null, jellyfinLastPlayed: null);
            Assert.AreEqual(JellyfinSyncAction.None, d.Action);
        }

        [TestMethod]
        public void EqualTimestamps_RegardWins_Pushes()
        {
            // Tie goes to Regard (>=), so we push rather than adopt — deterministic, no flip-flop.
            var d = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: 60, regardUpdated: T0,
                jellyfinPlayed: false, jellyfinPositionTicks: 90 * TicksPerSec, jellyfinLastPlayed: T0Utc);
            Assert.AreEqual(JellyfinSyncAction.PushToJellyfin, d.Action);
            Assert.AreEqual(60L * TicksPerSec, d.PushTicks);
        }

        [TestMethod]
        public void AdoptThenReport_IsStable_NoPingPong()
        {
            // Round 1: Jellyfin newer -> adopt (seconds + Jellyfin's timestamp).
            var d1 = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: 10, regardUpdated: Older,
                jellyfinPlayed: false, jellyfinPositionTicks: 200 * TicksPerSec, jellyfinLastPlayed: Newer);
            Assert.AreEqual(JellyfinSyncAction.AdoptPosition, d1.Action);
            Assert.AreEqual(200, d1.PositionSeconds);

            // Round 2: local now holds the adopted value stamped with Jellyfin's own timestamp, and Jellyfin
            // is unchanged. Equal timestamps -> push (a harmless idempotent write of the same value), and
            // crucially NOT an endless adopt/adopt loop.
            var d2 = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: d1.PositionSeconds, regardUpdated: d1.Timestamp,
                jellyfinPlayed: false, jellyfinPositionTicks: 200 * TicksPerSec, jellyfinLastPlayed: Newer);
            Assert.AreEqual(JellyfinSyncAction.PushToJellyfin, d2.Action);
            Assert.AreEqual(200L * TicksPerSec, d2.PushTicks);
        }

        [TestMethod]
        public void SubSecondJellyfinTicks_RoundDownToZero_NoAdopt()
        {
            // A tiny position (<1s) rounds to 0 seconds; with no Regard position either, it's a no-op.
            var d = JellyfinReconciler.Reconcile(
                regardWatched: false, regardPositionSeconds: null, regardUpdated: null,
                jellyfinPlayed: false, jellyfinPositionTicks: TicksPerSec / 2, jellyfinLastPlayed: Newer);
            Assert.AreEqual(JellyfinSyncAction.None, d.Action);
        }
    }
}
