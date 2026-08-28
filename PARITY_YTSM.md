# Regard vs ytsm — feature parity

Comparison of **Regard** (this repo; C#/.NET, Blazor WASM, yt-dlp) against its predecessor
**ytsm** (`github.com/chibicitiberiu/ytsm`; Python/Django, YouTube Data API + youtube-dl).
Based on a code inventory of both (ytsm at its current `master`) and a live feature test of Regard.

## TL;DR
Regard is a modern rewrite that **gained** things ytsm never had (multi-site via yt-dlp, no API
key/quota, per-subscription title filters, Jellyfin integration, SignalR live updates, `@handle`
support). Most of the ytsm gaps have now been closed — **User Settings UI**, **logout**,
**video duration**, **live job-progress notifications + a Job Log**, a **fully functional watch page**
(embed / watch-on-site / up-next), **subscription import** (OPML + URL list), an **Admin settings +
user-management surface** (registration toggle, global/per-user quotas, enable/disable, delete), and
**subtitle options** (user + per-subscription) all shipped (2026-08-27 – 08-28). What remains: a
**filename-pattern UI** and **password reset**.

Status key: ✅ done · ◻ still open.

---

## Features ytsm has that Regard is missing (ranked; status updated 2026-08-27)

1. **Video duration & rating** — ✅ *both done*. `Video.Duration` is mapped from the yt-dlp wrapper
   and shown as a `m:ss`/`h:mm:ss` badge (commit `5e5bdb4`). Rating now renders as a 5-star widget on
   the watch page when `Video.Rating` has a value (commit `c419e66`). Note the data source stays weak
   (YouTube dropped dislikes), so the value is often absent — the widget just hides itself then.

2. **Settings UI (User + Admin)** — ✅ *both done*. The `/settings` page (commit `911731f`) has download
   max-resolution, codec exclude lists, transcode, merge container, a raw `-f` override, a **Job Log**
   tab (`5e5bdb4`), and now a **Storage & quota** usage panel. The new admin-only `/admin` page
   (commit `536c707`) adds an allow-registrations toggle, global default per-user quotas (max videos +
   GB), job-history retention, and full **user management** (list with usage, promote/demote,
   enable/disable, per-user quota override, delete). Sync-cron and scheduler concurrency were
   deliberately left out — both are startup/appsettings-only and not safe to change at runtime.

3. ✅ **Watch page for non-downloaded videos.** Done (commit `c419e66`). A non-downloaded video now
   shows either a privacy-gated YouTube embed (`youtube-nocookie`, when the user opts in via the new
   default-off "allow embedding" setting) or a placeholder frame with **Download now** + **Watch on
   {site}** — the latter handles any yt-dlp domain, not just YouTube. Plus title, views, published
   date, duration, a rating widget, description, and an **Up next** queue (unwatched: same-sub →
   folder → all) with mark-watched on finish. ytsm's auto-advance was intentionally dropped
   (mark-watched only).

4. ✅ **Import subscriptions.** Done (commit `fbb2472`). The Add menu's Import dialog takes an **OPML**
   export or a **URL list**, mirrors OPML folder groups as Regard folders, rewrites YouTube feed URLs
   to channel URLs, and runs the batch as a background job (bell progress + per-URL Job Log) with a
   per-import auto-download toggle. (Export still absent — neither app has it.)

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

8. ✅ **Subtitle options in-UI.** Done (commit `db8f188`). Both the user Settings page (a Subtitles
   section: on/off with auto-captions, language mode, and format sub-options) and the per-subscription
   edit form (the same five as inherit/on/off overrides) now expose `Ytdl_WriteSubtitles`,
   `Ytdl_WriteAutoSub`, `Ytdl_AllSubs`, `Ytdl_SubFormat`, `Ytdl_SubLang`. Also fixed the arg emission
   (language/format only when subtitles are enabled; "all" mutually exclusive with a language list).

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
5. ✅ ~~Import (OPML/URL list)~~ — done (`fbb2472`): OPML + URL list, folder mirroring, background job.
6. ✅ ~~Admin settings~~ — done (`536c707`): `/admin` page — registration toggle, global + per-user
   quotas (with in-Settings usage transparency + clear download-block messages), and user management
   (promote/demote, enable/disable, delete). Sync-cron/concurrency intentionally excluded.
7. ✅ ~~Subtitle UI~~ — done (`db8f188`): user Settings + per-subscription, plus the arg-emission fix.
8. ◻ **Filename-pattern UI** (`download_file_pattern` is still backend-only) — **now the top remaining gap**.
9. ◻ **Password reset** (email flow).
</content>
