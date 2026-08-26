# Regard — Backlog & Gotchas

Durable list of feature ideas and known issues, so they survive across sessions.
(Current active work is tracked in the plan files; this is the "later" pile.)

## Feature ideas

### Keyword include/exclude filtering per subscription
Follow a specific *series* from a channel instead of every upload. Per-subscription
include/exclude filters (keyword list or regex) matched against the video title
(optionally description), so only matching videos enter the download window.

- **Motivating example:** `LastWeekTonight` posts both full main-segment episodes and
  shorter web/main-section cuts; the user only wants the full episodes. A title
  include-pattern (or exclude-pattern for the cuts) would express that.
- **Design questions (decide later):**
  - Filter at *discovery* (don't even create Video rows for non-matches) vs at
    *download* (track them but exclude from the window). Download-time filtering in
    `ProcessDownloadRules`'s candidate query is the least invasive and keeps history;
    discovery-time keeps the DB clean. Leaning download-time.
  - Regex vs simple keyword list (include-any / exclude-any). Regex is more powerful
    but a footgun in a UI; maybe both (keywords by default, regex opt-in).
  - New per-subscription options (Title include / Title exclude), scoped like the
    other `Subscriptions_*` options.
- **Where:** `VideoDownloaderService.ProcessDownloadRules` candidate filter (+ new
  options in `Options.csv`/`Options.cs`, + frontend fields). Check whether ytsm had
  anything similar to borrow from.

## Known issues / gotchas

### Playlist ordering can be reversed (verify during Plan 3)
Recollection: playlists were tricky because their order came out reversed. This
matters directly for the disk-bounded window's `DownloadOrder = Oldest`:
- A channel's "uploads" playlist comes back **newest-first** (playlist_index 1 =
  newest), so ordering by `PlaylistIndex` ascending is effectively newest-first, not
  oldest — the opposite of what `Playlist`/`ReversePlaylist` names imply.
- yt-dlp's `playlist_index`/entry order may also differ from the old youtube-dl
  behavior the `PlaylistIndex` logic was written against.
- **Action:** when implementing/verifying the oldest-first window (Plan 3), confirm
  what `Published` and `PlaylistIndex` actually contain for a channel vs a curated
  playlist, and that `Oldest` really yields oldest-first. Fix the `Playlist` /
  `ReversePlaylist` semantics if they're inverted.
