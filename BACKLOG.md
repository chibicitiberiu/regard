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
