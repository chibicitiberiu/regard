# Regard vs ytsm — feature parity

Comparison of **Regard** (this repo; C#/.NET, Blazor WASM, yt-dlp) against its predecessor
**ytsm** (`github.com/chibicitiberiu/ytsm`; Python/Django, YouTube Data API + youtube-dl).
Based on a code inventory of both (ytsm at its current `master`) and a live feature test of Regard.

## TL;DR
Regard is a modern rewrite that **gained** things ytsm never had (multi-site via yt-dlp, no API
key/quota, per-subscription title filters, Jellyfin integration, SignalR live updates, `@handle`
support). Most of the ytsm gaps have now been closed — **User Settings UI**, **logout**,
**video duration**, **live job-progress notifications + a Job Log**, and a **fully functional watch
page** (embed / watch-on-site / up-next) all shipped (2026-08-27 – 08-28). What remains:
**subscription import**, an **Admin settings** surface, **subtitle/filename-pattern UI**, and
**password reset**.

Status key: ✅ done · ◻ still open.

---

## Features ytsm has that Regard is missing (ranked; status updated 2026-08-27)

1. **Video duration & rating** — ✅ *both done*. `Video.Duration` is mapped from the yt-dlp wrapper
   and shown as a `m:ss`/`h:mm:ss` badge (commit `5e5bdb4`). Rating now renders as a 5-star widget on
   the watch page when `Video.Rating` has a value (commit `c419e66`). Note the data source stays weak
   (YouTube dropped dislikes), so the value is often absent — the widget just hides itself then.

2. **Settings UI (User + Admin)** — ✅ *User done* / ◻ *Admin open*. The `/settings` page now exists
   (commit `911731f`) with download max-resolution, codec exclude lists, transcode, merge container,
   and a raw `-f` override — plus a **Job Log** tab (`5e5bdb4`). **Admin** settings (allow-registrations,
   sync cron schedule, scheduler concurrency) still have no UI.

3. ✅ **Watch page for non-downloaded videos.** Done (commit `c419e66`). A non-downloaded video now
   shows either a privacy-gated YouTube embed (`youtube-nocookie`, when the user opts in via the new
   default-off "allow embedding" setting) or a placeholder frame with **Download now** + **Watch on
   {site}** — the latter handles any yt-dlp domain, not just YouTube. Plus title, views, published
   date, duration, a rating widget, description, and an **Up next** queue (unwatched: same-sub →
   folder → all) with mark-watched on finish. ytsm's auto-advance was intentionally dropped
   (mark-watched only).

4. ◻ **Import subscriptions.** ytsm bulk-imports from **OPML** (YouTube's export) and plain URL lists,
   with a target folder + download-config for the batch (`ytsm index.py:389-484`,
   `utils/subscription_file_parser.py`). Regard's Add menu has "Import (todo)" — unimplemented.
   (Neither has export.)

5. **Logout / password reset** — ✅ *logout done* / ◻ *password reset open*. The user dropdown now
   shows the signed-in name + a Log out action (commit `5e5bdb4`). Password reset (email flow) is
   still absent.

6. ✅ **Job-progress notifications in the UI.** Done (commit `5e5bdb4`). The notification bell now
   shows live in-progress jobs (progress bar + current step) via SignalR, plus recent messages and
   error toasts; a **Job Log** tab in Settings lists all jobs with full per-job logs. Generic across
   job types (download, sync, …), not just downloads — matching ytsm's job panel.

7. **Download format/quality + filename pattern in-UI** — ✅ *format/quality done* / ◻ *filename
   pattern open*. The Settings page exposes resolution cap, codec excludes, transcode, and a raw
   override (commit `911731f`). The `download_file_pattern` equivalent is still backend-only. Note
   the shipped default format is still uncapped (now user-capable via Settings).

8. ◻ **Subtitle options in-UI.** ytsm lets you toggle subtitles, languages, auto-subs, format. Regard
   downloads subtitles too (`DownloadVideoJob` `Ytdl_WriteSubtitles`, `--sub-langs`) but the
   options are backend-only with no UI.

## Roughly at parity
Nestable **folders**; **video search** (Regard: `VideoList.razor.cs` `SetQuery/OnQueryChanged` is
wired), **watched/downloaded filters**, **sort**, **pagination**; **mark watched**, **download**,
**delete files** per video; **auto-download** with per-sub count/size limits and order; **setup
wizard** + registration gating; **multi-user** per-user data; **thumbnail caching**; **Docker**
deployment; SQLite default (both) / Postgres (ytsm) / SQL-Server-migrations (Regard).
Folder moves: ytsm has jstree drag-and-drop but it's **not persisted server-side** (a known TODO),
so effectively both rely on an edit form to re-parent.

## Features Regard has that ytsm lacks
- **Multi-provider** — yt-dlp (YouTube + hundreds of sites) plus an RSS provider; ytsm is
  YouTube-only.
- **No API key / quota** — Regard gets metadata from yt-dlp; ytsm needs a Google Data API key
  (ships a shared default key, quota-limited).
- **Per-subscription include/exclude title filters** — Regard has this (edit-page Filters +
  `SubscriptionFilters`); ytsm has **no** title filtering (only limits/order). (The backlog's
  "check whether ytsm had anything similar" → it didn't.)
- **Jellyfin integration** — metadata export + watched-sync; ytsm has no Plex/Jellyfin.
- **SignalR (websockets)** for live tree/list updates vs ytsm's 1.5s polling.
- **`@handle` URLs**, modern Blazor SPA, single-container image.

## Remaining work (highest value first)
1. ✅ ~~Map yt-dlp `duration` → `Video`~~ — done (`5e5bdb4`).
2. ✅ ~~A Settings page + logout~~ — User Settings done (`911731f`), logout done (`5e5bdb4`).
   Remaining: an **Admin settings** surface (registrations, sync schedule, concurrency).
3. ✅ ~~Wire the notifications panel~~ — done as a generic job-progress + Job Log system (`5e5bdb4`).
4. ✅ ~~Watch page for non-downloaded videos~~ — done (`c419e66`): embed / watch-on-site / up-next.
5. ◻ **Import (OPML/URL list)** — port ytsm's parser; the "Import (todo)" menu item is already there.
   **(Now the top remaining gap.)**
6. ◻ **Subtitle + filename-pattern UI**, and the **Admin settings** page (see #2).
</content>
