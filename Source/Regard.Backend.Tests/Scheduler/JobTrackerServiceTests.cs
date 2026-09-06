using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Model;
using Regard.Backend.Services;

namespace Regard.Backend.Tests.Scheduler
{
    [TestClass]
    public class JobTrackerServiceTests
    {
        private static JobInfo Job(int retryCount, bool notify) =>
            new JobInfo { RetryCount = retryCount, Notify = notify };

        [TestMethod]
        public void TerminalFailure_Notifies_OnlyWhenFinalAttemptAndOptedIn()
        {
            // Final attempt of a user-facing job -> shows the failure card.
            Assert.IsTrue(JobTrackerService.ShouldPostTerminalFailure(Job(retryCount: 0, notify: true)));

            // Final attempt of an unattended background job (no UserId) must NOT post — that card would
            // broadcast to every non-admin. This is the bug the guard fixes.
            Assert.IsFalse(JobTrackerService.ShouldPostTerminalFailure(Job(retryCount: 0, notify: false)));

            // Retries still pending -> not terminal, regardless of Notify.
            Assert.IsFalse(JobTrackerService.ShouldPostTerminalFailure(Job(retryCount: 2, notify: true)));
            Assert.IsFalse(JobTrackerService.ShouldPostTerminalFailure(Job(retryCount: 2, notify: false)));
        }
    }
}
