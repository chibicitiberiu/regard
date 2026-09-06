using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using YoutubeDLWrapper;

namespace YoutubeDLWrapper.Tests
{
    [TestClass]
    public class IdleWatchdogTests
    {
        private const long MiB = 1024 * 1024;
        private const int IdleMs = 1000;

        private static long T(long ms) => ms * TimeSpan.TicksPerMillisecond;

        [TestMethod]
        public void NotIdleYet_DoesNotKill()
        {
            var w = new IdleWatchdog(IdleMs, () => 0, T(0));
            Assert.IsFalse(w.ShouldKill(T(500)));   // 500 ms < 1000 ms idle window
        }

        [TestMethod]
        public void IdlePastTimeout_NoProbe_Kills()
        {
            var w = new IdleWatchdog(IdleMs, null, T(0));
            Assert.IsTrue(w.ShouldKill(T(2000)));
        }

        [TestMethod]
        public void DisabledTimeout_NeverKills()
        {
            var w = new IdleWatchdog(0, null, T(0));
            Assert.IsFalse(w.ShouldKill(T(1_000_000)));
        }

        [TestMethod]
        public void Growing_Survives_AcrossWindows()
        {
            long cur = 0;
            var w = new IdleWatchdog(IdleMs, () => { cur += 2 * MiB; return cur; }, T(0));
            Assert.IsFalse(w.ShouldKill(T(2000)));  // +2 MiB this window
            Assert.IsFalse(w.ShouldKill(T(4000)));  // +2 MiB since the last reset
            Assert.IsFalse(w.ShouldKill(T(6000)));  // still growing
        }

        [TestMethod]
        public void Trickle_BelowThreshold_Kills()
        {
            long cur = 0;
            var w = new IdleWatchdog(IdleMs, () => { cur += 100; return cur; }, T(0));
            Assert.IsTrue(w.ShouldKill(T(2000)));   // +100 bytes/window < 1 MiB floor
        }

        [TestMethod]
        public void FlatAfterPartial_GetsOneWindow_ThenKills()
        {
            // A download that reached 5 MiB then stalled: init lastProbedSize=0 grants the first idle
            // window (5 MiB reads as growth), then the next window sees no growth and kills.
            var w = new IdleWatchdog(IdleMs, () => 5 * MiB, T(0));
            Assert.IsFalse(w.ShouldKill(T(2000)));  // first window: 5 MiB accumulated -> reprieve
            Assert.IsTrue(w.ShouldKill(T(4000)));   // no further growth -> killed
        }

        [TestMethod]
        public void ProbeError_MinusOne_Kills()
        {
            var w = new IdleWatchdog(IdleMs, () => -1, T(0));
            Assert.IsTrue(w.ShouldKill(T(2000)));   // unknown -> never resets -> falls back to kill
        }

        [TestMethod]
        public void NoteOutput_ResetsIdle()
        {
            var w = new IdleWatchdog(IdleMs, () => -1, T(0));
            w.NoteOutput(T(2000));
            Assert.IsFalse(w.ShouldKill(T(2500)));  // 500 ms since output
            Assert.IsTrue(w.ShouldKill(T(3100)));   // 1100 ms since output, probe -1 -> kill
        }
    }
}
