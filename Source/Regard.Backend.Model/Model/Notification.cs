using Regard.Backend.Model;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Regard.Backend.Common.Model
{
    public enum NotificationSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>What clicking the body of a notification does.</summary>
    public enum NotificationPrimaryAction
    {
        None,
        OpenVideo,
        OpenLogs
    }

    /// <summary>
    /// A user-facing notification — the bell. One model that unifies live job progress and discrete
    /// messages: a download starts <see cref="Ongoing"/> with <see cref="Progress"/>, then the same row
    /// (matched by <see cref="Key"/>) transitions to a terminal Success/Error with an action. Persisted,
    /// so the bell survives a reload.
    /// </summary>
    public class Notification
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// Stable upsert key (e.g. "job:{jobId}"). A background update replaces the row with the same
        /// (UserId, Key) instead of adding a new one.
        /// </summary>
        public string Key { get; set; }

        public UserAccount User { get; set; }

        /// <summary>Owner, or null for an ownerless system notification (broadcast to all clients).</summary>
        public string UserId { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public string Title { get; set; }

        public string Text { get; set; }

        public NotificationSeverity Severity { get; set; }

        /// <summary>Progress 0..1, or null for no bar / indeterminate while <see cref="Ongoing"/>.</summary>
        public float? Progress { get; set; }

        /// <summary>True while the operation is in flight (pinned, not dismissable).</summary>
        public bool Ongoing { get; set; }

        /// <summary>
        /// Video.Id for the OpenVideo / Retry actions. A plain int with no FK on purpose: the
        /// notification (and its Retry link) must survive the video's deletion. NOTE: this is the
        /// database Video.Id, NOT the provider's string Video.VideoId.
        /// </summary>
        public int? VideoDbId { get; set; }

        /// <summary>Owning job for OpenLogs. Plain long, no FK, so it survives job pruning.</summary>
        public long? JobId { get; set; }

        public NotificationPrimaryAction PrimaryAction { get; set; }

        /// <summary>True while the job is a live, cancellable download (drives the Cancel button).</summary>
        public bool Cancellable { get; set; }
    }

    /// <summary>
    /// A job's contribution to its notification: the human-facing title/body plus an optional action
    /// target. Computed by <c>JobBase</c> virtuals and threaded through the job-tracker calls, so
    /// notification emission stays centralized (JobBase has no NotificationService of its own).
    /// </summary>
    public class JobNotification
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public int? VideoDbId { get; set; }
        public NotificationPrimaryAction PrimaryAction { get; set; } = NotificationPrimaryAction.None;
    }
}
