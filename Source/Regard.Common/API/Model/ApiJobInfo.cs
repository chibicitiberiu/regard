using System;

namespace Regard.Common.API.Model
{
    /// <summary>
    /// Mirrors the backend JobState enum (same order, so an int cast maps cleanly).
    /// </summary>
    public enum ApiJobState
    {
        Created,
        Scheduled,
        Running,
        Completed,
        Failed,
        // Appended to stay aligned with the backend JobState int cast — never reorder.
        Cancelled
    }

    public class ApiJobInfo
    {
        /// <summary>
        /// Job id.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Name of job
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Current lifecycle state.
        /// </summary>
        public ApiJobState State { get; set; }

        /// <summary>
        /// Detail (i.e. current step)
        /// </summary>
        public string Detail { get; set; }

        /// <summary>
        /// Value between 0 and 1 indicating progress, or null when indeterminate.
        /// </summary>
        public float? Progress { get; set; }

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset? Started { get; set; }

        public DateTimeOffset? Completed { get; set; }

        /// <summary>
        /// True while this job can be cancelled by the user (a live download). Drives the bell's
        /// Cancel button. Set from the cancellation registry on each live push.
        /// </summary>
        public bool Cancellable { get; set; }

        /// <summary>
        /// Full captured output. Populated only by the job-detail endpoint, not by list/live pushes.
        /// </summary>
        public string Log { get; set; }
    }
}
