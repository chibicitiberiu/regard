# Regard vs ytsm — feature parity

Comparison of **Regard** (this repo; C#/.NET, Blazor WASM, yt-dlp) against its predecessor
**ytsm** (`github.com/chibicitiberiu/ytsm`; Python/Django, YouTube Data API + youtube-dl).
Based on a code inventory of both (ytsm at its current `master`) and a live feature test of Regard.

## TL;DR
Regard is a modern rewrite that **gained** things ytsm never had (multi-site via yt-dlp, no API
key/quota, per-subscription title filters, Jellyfin integration, SignalR live updates, `@handle`
support). But several ytsm features were **not carried over** — the biggest are a **Settings UI**,
a **usable watch page for non-downloaded videos**, **video duration/rating**, **import**, and
**logout**. Details below.

---

## Features ytsm has that Regard is missing (ranked)

1. **Video duration & rating.** ytsm pulls `duration`, `views`, and `rating` (from likes/dislikes)
   via the YouTube Data API and shows duration + a star-rating widget on every card
   (`ytsm models.py:182-185`, `templatetags/ratings.py`). Regard shows neither — its video model
   has no duration and doesn't render `Rating`.
   *Closable without the API:* Regard's own yt-dlp wrapper already parses it —
   `UrlInformation.cs:174 public double? Duration`. It just isn't mapped into the `Video` model or
   the card. (This is why the old UI faked a hardcoded "5:00", which I removed.)

2. **Settings UI (User + Admin).** ytsm has two settings pages: **User Settings** (download path,
   filename pattern, format, order, global/per-sub limits, subtitle options) and **Admin Settings**
   (YouTube API key, allow-registrations, sync cron schedule, scheduler concurrency)
   (`ytsm views/settings.py`, `forms/settings.py`). **Regard has no settings page at all** — the
   navbar gear links to a dead `/settings` route. All of Regard's equivalent options exist in the
   backend (`Options.cs`) but are only reachable per-subscription on the edit page; there is no
   global/user surface.

3. **Watch page for non-downloaded videos.** ytsm embeds the **YouTube IFrame player** (watch
   without downloading), a **"Watch on YouTube"** link, and a **"Watch All" up-next queue** with
   summed duration and auto-advance/auto-mark-watched (`ytsm video.html`, `views/video.py`).
   Regard's watch page for a not-yet-downloaded video is literally TODO stubs ("TODO: Show embedded
   video / View on original site / Download now"). Only already-downloaded videos play.

4. **Import subscriptions.** ytsm bulk-imports from **OPML** (YouTube's export) and plain URL lists,
   with a target folder + download-config for the batch (`ytsm index.py:389-484`,
   `utils/subscription_file_parser.py`). Regard's Add menu has "Import (todo)" — unimplemented.
   (Neither has export.)

5. **Logout.** ytsm has login/logout/registration + a full **password-reset** flow
   (`ytsm views/auth.py`, `templates/registration/*`). Regard has login/registration and a setup
   wizard, but the user dropdown is a placeholder ("This will be the user panel") — **no logout**,
   and no password reset (no email flow).

6. **Job-progress notifications in the UI.** ytsm surfaces running jobs live: a footer progress bar
   + a job panel listing each job's progress/messages, polled every 1.5s from `JobExecution`/
   `JobMessage` rows (`ytsm common.js`, `views/notifications.py`). Regard already has the plumbing
   (SignalR + backend job messages) but the notifications dropdown is a placeholder
   ("Notifications panel") — the progress is never shown. So a triggered download gives **no UI
   feedback** in Regard.

7. **Download format/quality + filename pattern configurable in-UI.** ytsm exposes `download_format`,
   `download_path` (with `${env:…}`), and a `download_file_pattern`
   (`${channel}/${playlist}/S01E${playlist_index} - ${title} [${id}]`). Regard has the equivalent
   options (`Ytdl_Format`, path) but no UI, and the format defaults to uncapped 4K.

8. **Subtitle options in-UI.** ytsm lets you toggle subtitles, languages, auto-subs, format. Regard
   downloads subtitles too (`DownloadVideoJob.cs:321` `Ytdl_WriteSubtitles`, `--sub-langs`) but the
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

## Suggested quick wins (highest value first)
1. **Map yt-dlp `duration` → `Video` (+ show it, and `Rating`).** The wrapper already has it; needs
   a model field + migration + card badge. Restores the badge I removed with real data.
2. **A Settings page + logout.** Even a thin User Settings (download format/quality cap, order,
   limits, path) + Admin (registrations, sync schedule) closes the largest gap and fixes the dead
   navbar links. Add a logout to the user dropdown.
3. **Wire the notifications panel** to the existing SignalR job messages so downloads show progress.
4. **Watch page**: at minimum a YouTube embed + "Watch on YouTube" + a "Download now" button for
   non-downloaded videos.
5. **Import (OPML/URL list)** — port ytsm's parser; the "Import (todo)" menu item is already there.
</content>
