# Regard — Backlog & Gotchas

Durable list of feature ideas and known issues, so they survive across sessions.
(Current active work is tracked in the plan files; this is the "later" pile.)

> **UX/feature feedback from the 2026-08-29 testing pass lives in
> [`UX_FEEDBACK.md`](UX_FEEDBACK.md)** — grouped by area with effort tags and a proposed
> 6-batch plan. Start there for the current polish/feature queue.

## Feature ideas

### ~~Keyword include/exclude filtering per subscription~~ — DONE (`89d27fd`)
Shipped as per-subscription Include/Exclude **regex** title filters with a preview UI,
filtered at download time in `VideoDownloaderService.ProcessDownloadRules`'s candidate
query (`SubscriptionFilterExtensions.PassesTitleFilters`) so history is kept. Covers the
`LastWeekTonight` full-vs-cuts case. (ytsm had no title filtering to borrow from.)

## Known issues / gotchas

### Playlist ordering — checked during the flat-sync rework (`ade34c9`), mostly resolved
Confirmed: a channel's "uploads" playlist comes back **newest-first**. The flat-sync
rework preserves that provider order (no `OrderBy(Published)` during ingest) and assigns
a **per-subscription** `PlaylistIndex`. Default `DownloadOrder = Newest` orders by
`Published`, which the eager-enriched newest videos have; deferred/flat videos get
`Published = MinValue` and sort last.
- **`DownloadOrder = Oldest` + flat sync:** un-enriched (flat) videos have
  `Published = MinValue`, so they sort *first* under Oldest and are picked first — which
  is correct (they genuinely are the older, back-catalog videos). Any un-enriched video is
  enriched with full metadata in `DownloadVideoJob` before the download, so the
  filename/season/NFO are right regardless of order.
- **Still worth a real-data check:** verify on the NAS that `Oldest` truly yields
  oldest-first for both a channel and a curated playlist, and that the `Playlist` /
  `ReversePlaylist` order options match their names against yt-dlp's `playlist_index`.

### Large downloads get killed by the idle watchdog while still writing — likely the source of the broken files
Observed live on 2026-08-30 (job 417, "Why Runways Have to Be Repainted", ~1 GB at `--limit-rate 2M`):
`Job failed: youtube-dl stalled: no output for 600053 ms`, while the `.part` on disk had grown from
256 MB to 754 MB during that window. The watchdog in `YoutubeDL.Run` (`YoutubeDL.cs:90-103`) measures
**stdout/stderr line activity only** (`lastOutput`, updated in `OutputProcessingThread:140`) and knows
nothing about the output file, so a download that is demonstrably progressing can be killed at
`Ytdl_IdleTimeout` (default **10 minutes**, `Options.cs:262-263`).

**Root cause not established.** yt-dlp does emit progress through a pipe with `--newline` when run
by hand, so it isn't simple block buffering; the real job passes many more args (`--limit-rate 2M`,
`--sleep-interval`, `--sleep-requests`) and downloads a fragmented `315+251-12` format. Needs its own
investigation — do not assume the cause.

**Why it matters:** this is a strong candidate for the *original* "download again failed, files were
missing or incomplete" report. It leaves exactly the orphaned `.part` files the dev library contains,
including an 832 MB one from 2026-08-29 that pre-dates this work. Candidate fixes: have the watchdog
also treat output-file growth as liveness, and/or raise the default. Batch 3 Phase 1 makes recovery
possible but does not address the cause.

### ~~`JobInfo.RetryCount` never decrements~~ — FIXED (`31c19fe`)
`JobRetryService` decremented a freshly-loaded copy, then `JobBase`'s finally-block persist wrote its
own stale instance back over it, so failed jobs retried forever and the card always read
"Retrying (1/3)". Fixed by mutating the shared instance. Verified against the database: a job failed
after the fix has RetryCount 2 and state Scheduled, while every pre-fix failure still sits at 3.


### 45 of the CGP Grey videos in the library are members-only and can never be downloaded
Found while testing Batch 5a. CGP Grey's `/videos` tab has 194 entries, **45** of which report
`availability: subscriber_only`; the library holds all 194, because they were ingested before the
content-scope filter existed. Without a channel membership in that user's `cookies.txt` they can be
listed but never fetched, so they sit there as permanent download failures.

The new "Include members-only videos" option (off by default) stops *new* ones being added and the sync
job logs the count, but nothing removes what's already stored — deleting rows is a destructive operation
that deserves its own opt-in. Candidates: a "remove out-of-scope videos" maintenance action, or marking
them `DownloadSkipped` so the auto-downloader stops trying.

### Sidebar tree doesn't highlight on a deep link
`SubscriptionTree`'s highlight is its own `treeView.SelectedItem`, which only its own click handler
sets. `AppState.SelectedSubscription` changes drive *navigation* (`AppController.cs:141-149`) but the
tree's listener (`SubscriptionTree.razor.cs:119-127`) only recomputes `isHomeActive`. So navigating to
`/subscription/5` directly — from the watch page's new uploader link, a bookmark, or a typed URL —
leaves the sidebar unhighlighted. Pre-existing; noted while adding that link (Batch 4a), which
deliberately does not paper over it by writing AppState (that would navigate twice and still not
highlight).

### `VideoOrder.Rating` ("Highest rated") quietly changed meaning
Inert before Batch 4a — `Video.Rating` was null for every YouTube video, so the sort did nothing. It's
now populated for videos whose watch page was opened while Return YouTube Dislike was on, so a handful
of arbitrary rows float to the top and everything else ties at null. Either backfill the ratio during
enrichment or hide the sort option until coverage is better.

### No JavaScript runtime for yt-dlp — YouTube extraction is running degraded
Every YouTube extraction currently logs:
`WARNING: [youtube] No supported JavaScript runtime could be found. … YouTube extraction without a JS
runtime has been deprecated, and some formats may be missing.` yt-dlp enables **deno** by default (see
its EJS wiki page). Neither the host nor the Docker image has it, so some formats are silently missing
from every extraction. Fixing it means installing deno (host: the user's call; image: a Dockerfile
line), and it should land alongside the `--impersonate` work since both are anti-bot/extraction
quality. Verified on 2026-08-30 with yt-dlp 2026.8.19.

## Known issues found during the live-update rework (2026-08-30), deliberately out of scope

- **DbContexts don't take `DbContextOptions`.** `DataContext(IConfiguration)` chains to the parameterless
  `DbContext()`, so anything configured through `AddDbContext`'s options lambda is **silently ignored** —
  provider config and interceptors must go through `OnConfiguring`. Converting to proper options ctors is
  the right shape but drags in both design-time factories and the migration tooling.
- **`UseLazyLoadingProxies()` never ran** and has now been removed: no derived context chained to
  `base.OnConfiguring`. It cannot simply be switched on — `Video.Subscription` isn't `virtual`, so proxy
  validation would throw at model build. Code needing a navigation must `Include` it or read the FK.
- **`BulkObservableCollection` / `ObservableDictionary` don't implement `INotifyCollectionChanged`**, so
  `ListView`'s subscription to it is dead for every consumer; lists only repaint because components call
  `StateHasChanged` themselves. Hence the "replace the element, don't mutate the DTO" rule.
- **`JobInfo.UserId` is never populated** — every job row is ownerless, because `RegardScheduler.Schedule`'s
  `userId` argument is essentially never passed. Job pushes therefore broadcast to all authenticated
  clients, which matches `JobsController.VisibleJobs` (non-admins already see `UserId == null` jobs).
  Populating it would allow per-user job pushes.
- **`UserLogger.Stop()`** `Join()`s a thread parked in `Monitor.Wait` with no timeout and a non-volatile
  stop flag — an existing shutdown hang risk.
- **SignalR has no backplane**, so live updates only reach clients connected to the same instance. Fine for
  a single-instance deployment; would need one if ever scaled out.
