using System.Collections.Concurrent;
using System.Threading;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Tracks live, cancellable download jobs so the API can cancel one by job id. Quartz uses a single
    /// durable JobKey per job type, so <c>scheduler.Interrupt</c> can't target one video's download; a
    /// registry of per-job cancellation contexts is the way to reach exactly one running download.
    /// Singleton.
    /// </summary>
    public class DownloadCancellationRegistry
    {
        /// <summary>Per-job cancellation state, shared between the running job and the cancel endpoint.</summary>
        public class CancelContext
        {
            public CancellationTokenSource Cts { get; } = new CancellationTokenSource();

            /// <summary>Set by the cancel endpoint so the job can tell a user cancel from a quota abort.</summary>
            public bool UserCancelled { get; set; }
        }

        private readonly ConcurrentDictionary<long, CancelContext> contexts = new();

        /// <summary>Registers a job's cancellation context on start. Returns the context to use.</summary>
        public CancelContext Register(long jobId)
        {
            var ctx = new CancelContext();
            contexts[jobId] = ctx;
            return ctx;
        }

        /// <summary>Removes a job's context when it finishes (call from a finally).</summary>
        public void Unregister(long jobId) => contexts.TryRemove(jobId, out _);

        /// <summary>Whether the job is currently live and cancellable.</summary>
        public bool IsCancellable(long jobId) => contexts.ContainsKey(jobId);

        /// <summary>
        /// Requests cancellation of a running download. Returns false if no such live job exists.
        /// </summary>
        public bool Cancel(long jobId)
        {
            if (!contexts.TryGetValue(jobId, out var ctx))
                return false;

            ctx.UserCancelled = true;
            ctx.Cts.Cancel();
            return true;
        }
    }
}
