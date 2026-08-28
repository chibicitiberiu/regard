# Regard — Backlog & Gotchas

Durable list of feature ideas and known issues, so they survive across sessions.
(Current active work is tracked in the plan files; this is the "later" pile.)

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
