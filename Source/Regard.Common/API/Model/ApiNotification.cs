using System;

namespace Regard.Common.API.Model
{
    /// <summary>Mirrors the backend NotificationSeverity enum (same order, so an int cast maps cleanly).</summary>
    public enum ApiNotificationSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>Mirrors the backend NotificationPrimaryAction enum (same order).</summary>
    public enum ApiNotificationAction
    {
        None,
        OpenVideo,
        OpenLogs
    }

    /// <summary>
    /// A user-facing notification pushed to the bell. Ongoing ones carry live progress; terminal ones
    /// carry an action (open the video / open the job logs) and optional Retry (when it's a failed
    /// download, i.e. Severity == Error and VideoId is set).
    /// </summary>
    public class ApiNotification
    {
        public long Id { get; set; }

        /// <summary>Stable key used to update a notification in place (e.g. "job:{jobId}").</summary>
        public string Key { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string Title { get; set; }

        public string Text { get; set; }

        public ApiNotificationSeverity Severity { get; set; }

        /// <summary>0..1, or null for no bar / indeterminate while Ongoing.</summary>
        public float? Progress { get; set; }

        public bool Ongoing { get; set; }

        /// <summary>Video.Id, for the OpenVideo / Retry actions (null when not video-related).</summary>
        public int? VideoId { get; set; }

        /// <summary>Owning job, for the OpenLogs action.</summary>
        public long? JobId { get; set; }

        public ApiNotificationAction PrimaryAction { get; set; }

        public bool Cancellable { get; set; }
    }
}
