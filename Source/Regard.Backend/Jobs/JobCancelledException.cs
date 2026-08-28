using System;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Thrown by a job to signal a user-requested cancellation. <see cref="JobBase"/> catches it and
    /// marks the job <c>Cancelled</c> (rather than <c>Failed</c>) with no retry.
    /// </summary>
    public class JobCancelledException : Exception
    {
        public JobCancelledException(string message = "Job cancelled.") : base(message) { }
    }
}
