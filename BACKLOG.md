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
- **Real-data check done (2026-09-07, dev CGP Grey channel, 194 videos, 146 enriched):**
  - **`Oldest` ✓** — enriched videos come back strictly oldest-first (published non-decreasing);
    un-enriched flat placeholders (`Published = MinValue`) sort first, which is correct here because
    they are all genuinely old back-catalog. (A *recent* un-enriched upload would mis-sort as "oldest",
    but eager enrichment of the newest N means recent uploads always have a real date, so it doesn't
    arise in practice.)
  - **`Playlist` / `ReversePlaylist` ✗ do NOT correspond to yt-dlp's `playlist_index`.** The provider
    (`YouTubeDLProvider`) does assign `PlaylistIndex = index++` in yt-dlp order, but
    `SynchronizeJob.FillVideoDetails` **overwrites** it with an internal `max+1` per-subscription
    **discovery counter** (`SynchronizeJob.cs:328-334`; the code even carries a `TODO: allow providers
    to set playlist indices`). So `Playlist` order is "order first seen by sync", which:
    - is **unstable across code versions**: this dev data has `pi=0` = *oldest* (seeded by an older
      oldest-first sync), but the current flat-sync lists **newest-first**, so a *freshly created*
      subscription would get `pi=0` = *newest* — the two subscriptions would sort oppositely under the
      same "Playlist order" option;
    - **does not track publication order** for the back-catalog: CGP Grey's 2011-2012 videos that were
      discovered later as flat entries carry the *highest* indices (`pi` 149-196), so under `Playlist`
      they land last and under `ReversePlaylist` first, regardless of their true age.
  - **Proposed fix (not done — larger, needs a real curated playlist to validate, i.e. the NAS):** the
    whole channel/playlist is already re-listed every sync, so re-derive `PlaylistIndex` from the
    provider's per-sync order on each full sync instead of appending `max+1`, and stop overwriting the
    provider index in `FillVideoDetails`. Must be validated against a genuinely **curated** playlist
    (where source order is user-meaningful and differs from chronological), plus a channel (where yt-dlp
    order is newest-first), and needs a migration/re-sync to fix existing indices. Deferred until there's
    real curated-playlist data to test against.

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


### The subtitle sweep re-queues a video that has no subtitles at all, forever
Found by letting Batch 5b's `RefreshMetadataJob` run on shipped defaults: every hourly pass logs
`queued 2 subtitle fetch(es)` and one of the two is always video 464 (*Yamaha Reface Review - Part 2*),
whose job completes with `yt-dlp returned no subtitles for this video`.

`SubtitleNeeds.NeedsSubtitles` answers "does this video have the configured languages on disk?", and for
a video YouTube has no captions for the answer is permanently no. So the sweep spends one throttled
extraction on it every hour and can never succeed. The other re-queued video (135, still missing `ro`
after a 429) is the *correct* case — that one should retry, and 136 completed on its own exactly that way.

The distinction to encode is "we asked and there was nothing" versus "we asked and it failed". Options:
record the last attempt on the video (a column, or reuse the sidecar convention with an empty marker
file) and apply a backoff; or have `ReprocessVideoJob` write a `.nosubs` sentinel next to the media that
`NeedsSubtitles` treats as satisfied. Either way the manual action should ignore the marker, so a user
click still re-checks.

### A members-only video fails its download three times before giving up
Noticed while testing Batch 5b: videos 187 and 193 (CGP Grey footnote/TL;DW bonus clips) are among the
45 members-only videos below, and asking to download one produces
`ERROR: [youtube] …: This video is available to this channel's members on level: … Join this channel…`
— then retries twice more on the standard 15-minute schedule before failing for good.

yt-dlp says exactly what is wrong, and it is not a transient condition, so the retry is pure waste (and
three extra requests against the bot gate). `DownloadVideoJob` already sets `Job.RetryCount = 0` for
other permanent failures such as an invalid video id; recognising the members-only message and doing the
same would fail it once, clearly. Related: `Video.ProviderAvailability` already carries
`subscriber_only` during sync, so the download job could refuse before spending a request at all.

**It gets worse across restarts.** `DownloadVideoJob` is `[ResumeAfterRestart]`, so a still-scheduled
attempt is re-queued on every boot — and with Batch 5b that queued download now legitimately holds the
throttle's download pressure, which makes `RefreshMetadataJob` stand down. Observed: *"Metadata refresh
deferred: youtube.com has 0 download(s) in flight and 2 queued"* on a fresh boot, where both queued
downloads were members-only videos that could never succeed. A permanently-failing download therefore
starves background maintenance indefinitely. Failing these fast fixes both symptoms at once.

### 45 of the CGP Grey videos in the library are members-only and can never be downloaded
Found while testing Batch 5a. CGP Grey's `/videos` tab has 194 entries, **45** of which report
`availability: subscriber_only`; the library holds all 194, because they were ingested before the
content-scope filter existed. Without a channel membership in that user's `cookies.txt` they can be
listed but never fetched, so they sit there as permanent download failures.

The new "Include members-only videos" option (off by default) stops *new* ones being added and the sync
job logs the count, but nothing removes what's already stored — deleting rows is a destructive operation
that deserves its own opt-in. Candidates: a "remove out-of-scope videos" maintenance action, or marking
them `DownloadSkipped` so the auto-downloader stops trying.

### ~~Sidebar tree doesn't highlight on a deep link~~ — DONE
`SubscriptionTree`'s highlight is its own `treeView.SelectedItem`, which only its own click handler
sets. `AppState.SelectedSubscription` changes drive *navigation* (`AppController.cs:141-149`) but the
tree's listener only recomputed `isHomeActive`. So navigating to `/subscription/5` directly — from the
watch page's uploader link, a bookmark, or a typed URL — left the sidebar unhighlighted.

Fixed by making the highlight follow the **route** (the real "what am I viewing"), not AppState. The
tree subscribes to `NavigationManager.LocationChanged` and, after each build, parses the URL
(`/subscription/{id}`, `/folder/{id}`, plus their `/edit/{id}` forms) to `treeView.SelectedItem` via
`ResolveRouteNode` → `treeSubs`/`treeFolders`. It's display-only: a `syncingSelectionFromRoute` flag
suppresses `OnSelectedItemChanged` during the sync, so it never writes AppState or double-navigates.
Covers deep-links, back/forward, and the watch-page uploader link; direct tree clicks still select and
navigate as before. `SubscriptionTree` now `IDisposable` to unhook the handlers. Playwright-verified
(deep-link sub/folder highlight, selection moves between subs, Home deselects, click still works, no JS
errors).

### ~~`VideoOrder.Rating` ("Highest rated") quietly changed meaning~~ — DONE
Replaced the sparse RYD like-ratio sort with a like-COUNT sort: the enum member `Rating` was renamed to
`MostLiked` (int value preserved, so stored DownloadOrder rows are unaffected) and now orders by
`Video.Likes`, which enrichment populates for every video (yt-dlp `like_count`) — broad coverage instead
of the handful of RYD-rated rows. Labelled "Most liked" everywhere. `Video.Rating` still drives the
watch page's like/dislike ratio. Verified: the API sort returns CGP Grey videos by likes descending, and
the sort menu + Settings show "Most liked".

### ~~No JavaScript runtime for yt-dlp~~ — DONE (`30af97a`)
yt-dlp needs a JS runtime (deno) or YouTube extraction degrades and some formats go missing. Resolved
in `30af97a`: the Docker image installs the static deno binary to `/usr/local/bin` (arch-aware amd64/
arm64, with a build-time `deno --version` check), and `run-debug.sh` puts a userspace `~/.deno/bin` on
PATH for local runs. Verified 2026-09-07: host has deno 2.9.6, the running dev backend logs no
"No supported JavaScript runtime" warning, and the image's deno release URL resolves. (Installing deno
on a bare production host, if not using the image, stays the user's call.)

### A failing background job notifies every user on the server (2026-09-03)

`JobTrackerService.OnJobFailed` posts a notification on terminal failure **without checking
`job.Notify`** — only the retry branch (`RetryCount > 0`) checks it. And a job scheduled with no
`userId`, which every recurring job is, gets `Notification.UserId = null`; `NotificationService.GetRecent`
matches `n.UserId == userId || n.UserId == null` for non-admins and `Send` uses `Clients.All`. So when
`FetchThumbnailsJob` or the deletions sweep fails, every account on the install gets a card about it.

Pre-existing and live today. `MaintenanceJob` (Batch 6a) works around it locally by never letting an
exception escape `ExecuteJob`, which is why an unattended sweep dying on a full disk stays quiet. The
real fix is to gate the `RetryCount <= 0` branch on `job.Notify` like the other one, but that changes
behaviour for every job type at once — worth doing deliberately rather than as a side effect.

### ~~SponsorBlock settings are missing from the folder page (2026-08-31)~~ — DONE

`Options.Sponsorblock_Actions` carries `OptionFlags.SubscriptionFolder`, and the resolver walks the
folder level, but `Pages/FolderEdit.razor` had no SponsorBlock field — so the middle level of the
inheritance chain could only be set through the DB.

Fixed by mirroring the subscription page: added `SponsorblockActions` to `SubscriptionFolderEditRequest`
and `ApiSubscriptionFolderConfig`, read it back in `SubscriptionFolderController.AddConfigs`
(`GetForSubscriptionFolderNoResolve`), and wrote it in `Edit` with the `Set/UnsetForSubscriptionFolder`
pair plus the same `HasRemoveSkipConflict` guard the subscription edit has (validated before anything
persists). UI: `<SponsorBlockEditor AllowInherit="true" @bind-Value="Request.SponsorblockActions" />` on
`FolderEdit.razor`, loaded from `Folder.Config`. Verified: API (conflict rejected without persisting,
valid set persists at folder scope, `none` distinct from inherit, inherit unsets) and Playwright
(field renders, Inherit↔Custom round-trips through save+reload, no JS errors).

**Still open — other folder-capable options with no folder-page field** (26 in all carry
`OptionFlags.SubscriptionFolder`; only 7 are surfaced now). The subscription page already has a
copyable UI+DTO+controller pattern for: `Ytdl_WriteSubtitles`, `Ytdl_WriteAutoSub`, `Ytdl_AllSubs`,
`Ytdl_SubFormat`, `Ytdl_SubLang`, `Subscriptions_DeleteGracePeriod`, `Subscriptions_IncludeShorts`,
`Subscriptions_IncludeMembersOnly`, `Subscriptions_PublishedAfter`, `Subscriptions_PublishedBefore`.
The remaining `Ytdl_*` (format/codec/transcode/write-metadata/limit-rate/retries) aren't on either edit
page today (admin/global-only in the UI). Left as a separate follow-up — not part of the SponsorBlock fix.

### ~~The watch page re-fetches SponsorBlock on every load (2026-08-31)~~ — DONE

`VideoController.List` called sponsor.ajay.app on each single-video fetch, with no caching, so opening
the same video five times was five requests.

Fixed with a short in-memory cache in `SponsorBlockClient.GetSkipSegmentsCached` (backed by
`IMemoryCache`, registered via `AddMemoryCache`), keyed on video id + the category set, 5-minute TTL.
The watch fetch (`VideoController`) now goes through it. Two things keep it safe: (1) the cached list is
the **raw** all-category fetch with `Skip` unset — the controller marks `Skip` from the current config
*after* the cache read, so config changes still take effect on a cache hit; (2) each call returns fresh
copies, so the controller mutating `Skip`/reordering can't corrupt the cached list. A no-segments result
is cached too (that's the common case worth not re-fetching), so a transient upstream failure is
suppressed for up to the TTL and self-heals. **Not** persisted on `Video` (the note stands — this data
legitimately moves; the `SponsorSegmentsRemoved` snapshot remains the only persisted copy). Download-time
`GetRemovedSegments` deliberately still uses the uncached `GetSkipSegments` and always sees fresh data.

Verified: 5 unit tests (repeat lookup served from cache, distinct video fetched separately, callers get
independent copies so a mutated `Skip` doesn't leak, empty result cached, empty video/categories never
hits the network); API triple-fetch returns byte-identical segments with correct per-config `skip`; and
Playwright (watch page renders the segment panel identically across reloads, no JS errors).

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
- ~~**`UserLogger.Stop()`** `Join()`s a thread parked in `Monitor.Wait` with no timeout and a non-volatile
  stop flag — an existing shutdown hang risk.~~ **FIXED (Batch 6a).** It was not just a risk: the thread
  was also a *foreground* thread, so any fatal startup failure left the process hanging forever after
  `Program.Main` returned instead of exiting. Found because the new pre-migration backup refusal aborted
  startup correctly and the process then never died. Now background, `volatile` flag, timed wait, pulsed
  and bounded join.
- **SignalR has no backplane**, so live updates only reach clients connected to the same instance. Fine for
  a single-instance deployment; would need one if ever scaled out.
