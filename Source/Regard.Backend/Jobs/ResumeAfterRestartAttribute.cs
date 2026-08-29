using System;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Marks a job type as safe to re-enqueue after a backend restart. Quartz here uses an in-memory
    /// trigger store, so a restart strands non-terminal jobs with no trigger to ever fire them; the
    /// startup reconciliation sweep (see <c>InitJob</c>) resumes types carrying this attribute from their
    /// persisted <c>JobInfo</c> row and abandons the rest — the recurring/maintenance jobs that a fresh
    /// periodic run covers anyway (sync, thumbnails, deletions sweep, ytdl update, Jellyfin poll).
    ///
    /// Only opt in a type that is idempotent on re-run: its payload lives in the persisted
    /// <c>JobInfo.JobData</c>, so resume just fires a fresh trigger against the same row. Inherited, so
    /// subclasses of a marked job (the <c>DeleteFilesJob</c> family) are resumable too.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ResumeAfterRestartAttribute : Attribute
    {
    }
}
