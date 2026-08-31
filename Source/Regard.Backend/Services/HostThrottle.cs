using Microsoft.Extensions.DependencyInjection;
using Regard.Backend.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Per-hosting-domain download/extraction throttle. Downloads use a non-blocking reserve
    /// (<see cref="TryReserveDownload"/>): if the host is busy/paced/capped the caller reschedules instead
    /// of holding a worker. Extractions use a short in-line pace. All state is per host, so different
    /// domains are independent (and, once the job pool is &gt; 1, run in parallel). Thread-safe.
    /// </summary>
    public class HostThrottle
    {
        private class HostState
        {
            public readonly object Sync = new object();
            public int InFlight;
            public DateTimeOffset NextAllowedUtc = DateTimeOffset.MinValue;   // pacing floor for the next op
            public readonly List<DateTimeOffset> DownloadTimes = new();       // for hour/day caps
            public readonly List<int> Queued = new();                        // video ids currently deferred (ordered)
        }

        private class Snapshot
        {
            public bool Enabled;
            public int DlMin, DlMax, ExMin, ExMax, MaxHour, MaxDay, PerHostConc;
            public DateTimeOffset TakenUtc;
        }

        public class HostStatus
        {
            public string Host { get; set; }
            public int InFlight { get; set; }
            public int Queued { get; set; }
            public int UsedLastHour { get; set; }
            public int UsedLastDay { get; set; }
            public DateTimeOffset? NextSlotUtc { get; set; }
        }

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ConcurrentDictionary<string, HostState> hosts = new();
        private readonly ConcurrentDictionary<int, byte> known = new();   // scheduled/in-flight video ids (dedup)
        private Snapshot cached;
        private readonly object snapLock = new object();

        public HostThrottle(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory;
        }

        private Snapshot Opt()
        {
            var c = cached;
            if (c != null && (DateTimeOffset.UtcNow - c.TakenUtc) < TimeSpan.FromSeconds(5))
                return c;

            lock (snapLock)
            {
                if (cached != null && (DateTimeOffset.UtcNow - cached.TakenUtc) < TimeSpan.FromSeconds(5))
                    return cached;

                using var scope = scopeFactory.CreateScope();
                var om = scope.ServiceProvider.GetRequiredService<IOptionManager>();
                cached = new Snapshot
                {
                    Enabled = om.GetGlobal(Options.Server_Throttle_Enabled),
                    DlMin = om.GetGlobal(Options.Server_Throttle_DownloadMinSeconds),
                    DlMax = om.GetGlobal(Options.Server_Throttle_DownloadMaxSeconds),
                    ExMin = om.GetGlobal(Options.Server_Throttle_ExtractMinSeconds),
                    ExMax = om.GetGlobal(Options.Server_Throttle_ExtractMaxSeconds),
                    MaxHour = om.GetGlobal(Options.Server_Throttle_MaxPerHour),
                    MaxDay = om.GetGlobal(Options.Server_Throttle_MaxPerDay),
                    PerHostConc = Math.Max(1, om.GetGlobal(Options.Server_Throttle_PerHostConcurrency)),
                    TakenUtc = DateTimeOffset.UtcNow,
                };
                return cached;
            }
        }

        private HostState State(string host) => hosts.GetOrAdd(host ?? "unknown", _ => new HostState());

        private static int Jitter(int minSec, int maxSec)
        {
            if (maxSec <= minSec) return Math.Max(0, minSec);
            return Random.Shared.Next(minSec, maxSec + 1);
        }

        private static void Prune(HostState st, DateTimeOffset now)
        {
            var cutoff = now - TimeSpan.FromDays(1);
            st.DownloadTimes.RemoveAll(t => t < cutoff);
        }

        private static int CountSince(HostState st, DateTimeOffset since) => st.DownloadTimes.Count(t => t >= since);

        // ---- dedup (prevent double-scheduling the same video) ----
        public bool IsKnown(int videoId) => known.ContainsKey(videoId);
        public void MarkKnown(int videoId) => known[videoId] = 1;
        public void ClearKnown(int videoId) => known.TryRemove(videoId, out _);

        /// <summary>
        /// Drops a video from the wait queue and the dedup set without it ever having run — for a
        /// download cancelled while it was still queued.
        ///
        /// Needed because the usual cleanup path doesn't cover this: a deferred job returns from
        /// ShouldDefer without reaching OnAfterExecute, so nothing releases the queue entry or the
        /// "known" marker. Left behind, the entry inflates every later queue-position message and the
        /// marker blocks the video from ever being auto-downloaded again.
        /// </summary>
        public void Dequeue(string host, int videoId)
        {
            var st = State(host);
            lock (st.Sync)
            {
                st.Queued.Remove(videoId);
            }
            ClearKnown(videoId);
        }

        /// <summary>
        /// Try to claim a download slot for <paramref name="host"/>. Returns true (slot reserved — caller
        /// must call <see cref="ReleaseDownload"/> when done) or false with <paramref name="retryAt"/> set
        /// to when the caller should reschedule.
        /// </summary>
        public bool TryReserveDownload(string host, int videoId, out DateTimeOffset retryAt)
        {
            retryAt = default;
            var opt = Opt();
            var st = State(host);
            var now = DateTimeOffset.UtcNow;

            lock (st.Sync)
            {
                if (!opt.Enabled)
                {
                    st.Queued.Remove(videoId);
                    st.InFlight++;
                    st.DownloadTimes.Add(now);
                    return true;
                }

                Prune(st, now);

                if (opt.MaxDay > 0 && CountSince(st, now - TimeSpan.FromDays(1)) >= opt.MaxDay)
                {
                    var oldest = st.DownloadTimes.DefaultIfEmpty(now).Min();
                    retryAt = oldest.AddDays(1);
                    EnsureQueued(st, videoId);
                    return false;
                }
                if (opt.MaxHour > 0 && CountSince(st, now - TimeSpan.FromHours(1)) >= opt.MaxHour)
                {
                    var oldestInHour = st.DownloadTimes.Where(t => t >= now - TimeSpan.FromHours(1)).DefaultIfEmpty(now).Min();
                    retryAt = oldestInHour.AddHours(1);
                    EnsureQueued(st, videoId);
                    return false;
                }
                if (st.InFlight >= opt.PerHostConc)
                {
                    retryAt = now.AddSeconds(Jitter(opt.DlMin, opt.DlMax));
                    EnsureQueued(st, videoId);
                    return false;
                }
                if (now < st.NextAllowedUtc)
                {
                    retryAt = st.NextAllowedUtc;
                    EnsureQueued(st, videoId);
                    return false;
                }

                // Reserve.
                st.Queued.Remove(videoId);
                st.InFlight++;
                st.NextAllowedUtc = now.AddSeconds(Jitter(opt.DlMin, opt.DlMax));
                st.DownloadTimes.Add(now);
                return true;
            }
        }

        private static void EnsureQueued(HostState st, int videoId)
        {
            if (!st.Queued.Contains(videoId))
                st.Queued.Add(videoId);
        }

        public void ReleaseDownload(string host)
        {
            var st = State(host);
            lock (st.Sync)
            {
                if (st.InFlight > 0)
                    st.InFlight--;
            }
        }

        /// <summary>1-based position of a deferred video in its host's queue, or 0 if not queued.</summary>
        public int QueuePosition(string host, int videoId)
        {
            var st = State(host);
            lock (st.Sync)
            {
                int idx = st.Queued.IndexOf(videoId);
                return idx < 0 ? 0 : idx + 1;
            }
        }

        public DateTimeOffset? NextSlotEta(string host)
        {
            var st = State(host);
            lock (st.Sync)
            {
                return st.NextAllowedUtc == DateTimeOffset.MinValue ? (DateTimeOffset?)null : st.NextAllowedUtc;
            }
        }

        /// <summary>
        /// True when a download on <paramref name="host"/> is running or waiting for a slot.
        ///
        /// This is how background maintenance yields to real work. Extractions and downloads share
        /// <c>NextAllowedUtc</c>, so every extraction pushes the pacing floor forward and makes a queued
        /// download defer again — a long extraction pass can starve downloads indefinitely without ever
        /// tripping the hour/day caps, which only count downloads. A low-priority job checks this before
        /// it starts and between items, and steps aside.
        /// </summary>
        public bool HasDownloadPressure(string host)
        {
            var st = State(host);
            lock (st.Sync)
            {
                return st.InFlight > 0 || st.Queued.Count > 0;
            }
        }

        /// <summary>Short in-line pace before a metadata extraction on <paramref name="host"/>.</summary>
        public async Task PaceExtractionAsync(string host, CancellationToken ct = default)
        {
            var opt = Opt();
            if (!opt.Enabled)
                return;

            TimeSpan wait;
            var st = State(host);
            var now = DateTimeOffset.UtcNow;
            lock (st.Sync)
            {
                var earliest = st.NextAllowedUtc == DateTimeOffset.MinValue ? now : st.NextAllowedUtc;
                wait = earliest > now ? earliest - now : TimeSpan.Zero;
                // Reserve the next extract slot so concurrent extractions on this host still space out.
                st.NextAllowedUtc = (earliest > now ? earliest : now).AddSeconds(Jitter(opt.ExMin, opt.ExMax));
            }
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
        }

        public IReadOnlyList<HostStatus> GetStatus()
        {
            var now = DateTimeOffset.UtcNow;
            var result = new List<HostStatus>();
            foreach (var kv in hosts)
            {
                var st = kv.Value;
                lock (st.Sync)
                {
                    Prune(st, now);
                    result.Add(new HostStatus
                    {
                        Host = kv.Key,
                        InFlight = st.InFlight,
                        Queued = st.Queued.Count,
                        UsedLastHour = CountSince(st, now - TimeSpan.FromHours(1)),
                        UsedLastDay = st.DownloadTimes.Count,
                        NextSlotUtc = st.NextAllowedUtc == DateTimeOffset.MinValue ? (DateTimeOffset?)null : st.NextAllowedUtc,
                    });
                }
            }
            return result;
        }
    }
}
