using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public abstract class JobBase : IJob
    {
        protected readonly ILogger log;
        protected readonly DataContext dataContext;
        protected readonly JobTrackerService jobTrackerService;

        // Per-run captured output, flushed to Job.Log on completion. Guarded because output pumps
        // (e.g. DownloadVideoJob.ProcessStdout runs on the yt-dlp reader thread) append concurrently.
        private readonly StringBuilder logBuffer = new StringBuilder();
        private readonly object logLock = new object();
        private const int MaxLogChars = 64 * 1024;

        protected JobInfo Job { get; set; }

        public JobBase(ILogger log,
                       DataContext dataContext,
                       JobTrackerService jobTrackerService)
        {
            this.log = log;
            this.dataContext = dataContext;
            this.jobTrackerService = jobTrackerService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            if (context.MergedJobDataMap.ContainsKey("JobId"))
                Job = dataContext.Jobs.Find(context.MergedJobDataMap.GetLong("JobId"));

            if (Job == null)
                throw new ArgumentException("Invalid job ID");

            jobTrackerService.OnJobStarted(Job, GetOngoingNotification());

            try
            {
                await ExecuteJob(context);
                jobTrackerService.OnJobCompleted(Job, GetSuccessNotification());
            }
            catch (JobCancelledException)
            {
                // User-requested cancellation: terminal, not a failure, and never retried.
                JobLog("Job cancelled.", MessageSeverity.Warning);
                jobTrackerService.OnJobCancelled(Job);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "{0} failed with exception!", GetType().Name);
                JobLog($"Job failed: {ex.Message}", MessageSeverity.Error);
                jobTrackerService.OnJobFailed(Job, ex.Message, null, GetFailureNotification(ex));
            }
            finally
            {
                PersistJobState();
            }
        }

        /// <summary>
        /// Reports fine-grained progress for the running job (0..1) with an optional step label.
        /// </summary>
        protected void ReportProgress(float fraction, string detail = null)
        {
            Job.Progress = fraction;
            Job.Detail = detail;
            jobTrackerService.OnJobProgress(Job.Id, fraction, detail, GetOngoingNotification());
        }

        #region User-facing notification hooks

        /// <summary>
        /// Title/body for the live "in progress" notification. Default is the job's name + current step;
        /// override to give a friendlier label (e.g. "Downloading" / a video title).
        /// </summary>
        protected virtual JobNotification GetOngoingNotification()
            => new JobNotification { Title = Job?.Name, Text = Job?.Detail };

        /// <summary>
        /// The terminal notification to show on success, or null to silently clear the in-progress one.
        /// Override for jobs with a meaningful outcome (a finished download, a completed import).
        /// </summary>
        protected virtual JobNotification GetSuccessNotification() => null;

        /// <summary>
        /// The terminal notification to show on the FINAL failure, or null to fall back to a generic
        /// "{job}: {reason}" message. Called from the catch, where the job's entity may not be loaded —
        /// overrides must null-guard their state and return null in that case.
        /// </summary>
        protected virtual JobNotification GetFailureNotification(Exception ex) => null;

        #endregion

        /// <summary>
        /// Appends a line to the per-run job log (shown later in the Settings Job Log). Thread-safe.
        /// </summary>
        protected void JobLog(string line, MessageSeverity severity = MessageSeverity.Info)
        {
            if (string.IsNullOrEmpty(line))
                return;

            lock (logLock)
            {
                logBuffer.Append(DateTimeOffset.Now.ToString("HH:mm:ss")).Append("  ");
                if (severity == MessageSeverity.Error)
                    logBuffer.Append("[ERROR] ");
                else if (severity == MessageSeverity.Warning)
                    logBuffer.Append("[WARN] ");
                logBuffer.Append(line).Append('\n');

                if (logBuffer.Length > MaxLogChars)
                    logBuffer.Remove(0, logBuffer.Length - MaxLogChars);
            }
        }

        /// <summary>
        /// Persists the job's final State/timestamps/Log. Load-bearing: JobTrackerService's OnJob*
        /// handlers save on a fresh scope that doesn't track this Job, so without this a job's state
        /// would never reach the DB and the Job Log would show it stuck at Created/Running.
        /// </summary>
        private void PersistJobState()
        {
            try
            {
                lock (logLock)
                {
                    if (logBuffer.Length > 0)
                        Job.Log = logBuffer.ToString();
                }

                dataContext.Jobs.Update(Job);
                dataContext.SaveChanges();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to persist job state/log for job {0}", Job?.Id);
            }
        }

        protected abstract Task ExecuteJob(IJobExecutionContext context);
    }
}
