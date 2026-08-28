# Regard — feature test findings (2026-08-27)

Method: headed Chromium (Playwright) driving the live app, backend :9585 + frontend :5000
(`run-debug.sh`, deno on PATH), fresh DB. Backend + browser logs monitored throughout.
DB was reset for this pass; backup at `~/regard-dev-backup-20260827-153516.tar.gz`.
Admin account created via wizard: **admin / Regard!2026**.

Legend: **[BUG]** defect · **[UX]** usability · **[MISSING]** unimplemented · **[PERF]** performance.
✅ = fixed and committed this pass.

---

## Bugs fixed this pass (committed)

| # | Fix | Commit |
|---|-----|--------|
| 1 | **App crashed on any unknown route** (e.g. the navbar's `/settings` link) with `Authorization requires a cascading parameter of type Task<AuthenticationState>`. `App.razor` never wrapped the Router in `<CascadingAuthenticationState>`. Now degrades to a graceful "nothing here". | `e46a2bf` |
| 2 | **Video download menu branches inverted** — a not-downloaded video offered "Delete downloaded files" and a downloaded one offered "Download", so you could never download a video from the menu. Swapped. | `cdb6a8d` |
| 3 | **Fake hardcoded "5:00" duration** on every video card (no duration exists in the model). Removed the misleading badge. | `cdb6a8d` |
| 4 | **New folders didn't appear until reload** — `FolderCreateForm` never refreshed the tree. Added `RequestRefresh()` (mirrors the subscription flow). | `cdb6a8d` |

(Earlier in the session, also fixed: subscriptions not appearing until Refresh — `SubscriptionTree` load ordering; `DropDownPanel.DisposeAsync` NRE; RSS content-type probing; yt-dlp members-only resilience; deno wiring. All verified live in this pass.)

---

## Fixed in later passes (2026-08-27 to 08-28)

| Item (was) | Fix | Commit |
|---|---|---|
| **No global Settings page** | Built the user Settings page (`/settings`) — max-resolution, codec exclude lists, transcode, raw `-f` override. Also surfaces download quality/format controls (closes "quality not configurable"). | `911731f` |
| **Download quality/format not configurable in UI** | Same Settings page; per-user `Ytdl_*` overrides. Default is still uncapped but now user-capable. | `911731f` |
| **No logout / user panel** | NavMenu user dropdown now shows the signed-in name + a Log out action. | `5e5bdb4` |
| **Notifications panel placeholder** | Real notification bell: live in-progress jobs (progress bar + step) and recent messages, fed by SignalR. | `5e5bdb4` |
| **No download progress indication** | Generic job-progress system: downloads/syncs stream live progress into the bell; a Job Log tab in Settings shows history + full per-job logs. | `5e5bdb4` |
| **Watch page hardcoded imgur channel icon** | Fetches the subscription and shows its real thumbnail (placeholder icon fallback). | `5e5bdb4` |
| **Empty state is a blank void** | Empty-state guidance in the video list and subscription tree. | `5e5bdb4` |
| **Modals don't close on Escape** | Escape-to-close via a keydown registry + Modal interop. | `5e5bdb4` |
| **Fake hardcoded "5:00" duration** (re-addressed) | Real `Video.Duration` from yt-dlp, shown as a `m:ss`/`h:mm:ss` badge. | `5e5bdb4` |
| **Watch page for non-downloaded videos was all TODO stubs** | Full watch page: a privacy-gated YouTube embed (`youtube-nocookie`) when the user opts in, otherwise a placeholder frame with **Download now** + **Watch on {site}** (handles any yt-dlp domain). Adds title, metric views, relative date, duration, rating stars, linkified description, an **Up next** queue (same-sub → folder → all), and mark-watched on finish. New per-user "allow embedding" setting (default off). | `c419e66` |
| **"Import (todo)" menu item did nothing** | Real importer: paste a URL list or upload an OPML export; OPML folder groups are mirrored as Regard folders, YouTube feed URLs are rewritten to channel URLs. Runs as a background job (progress in the bell, per-URL results in the Job Log); a dialog checkbox sets per-sub auto-download, duplicates skipped by default. | `fbb2472` |
| **No Admin/server settings UI** | New role-gated `/admin` page: allow-registration toggle, global default per-user quotas (max videos + GB), job-history retention, and full user management (list with live usage, promote/demote, enable/disable, per-user quota override, delete via a background teardown job). Quotas made transparent — a Storage & quota panel on Settings, and clear "quota reached" messages in the Job Log when a download is blocked. `disable` now actually blocks login (added a lockout check to the manual login path). | `536c707` |
| **Subtitle options backend-only** | Subtitle controls now in the UI: a Subtitles section on user Settings (on/off + auto-captions, language mode, format) and the same five as per-subscription overrides on the edit form. Also fixed the yt-dlp arg emission (language/format only when subtitles are on; "all languages" mutually exclusive with a specific list — it was previously overridden by the default `--sub-langs en`). | `db8f188` |
| **Filename pattern backend-only** | The download path/filename template (`Subscriptions_DownloadPath`, the yt-dlp `-o` template) is now a per-user default on Settings — it was already editable per-subscription. Both surfaces document the tokens (`{DownloadDirectory}`, `{FolderPath}`, `{Subscription.Name}`, `{EpisodeCode}`, `{Video.Name}`, …) and show a live example filename that updates as you type. Verified the user default feeds the real download (`-o …/CUSTOMDIR/S2011E0 - …`). | `170fd3d` |

---

## Verified working
- **Setup wizard** — Welcome → Prerequisites → Admin, "Step N of 4", creates the admin and logs in.
- **Add subscription** — modal (Create always enabled, spinner, resolves via yt-dlp), and the new sub now appears in the tree immediately.
- **Duplicate-subscription warning** — adding an already-subscribed channel shows the amber "You're already subscribed to …. Create another anyway?" banner with a "Create anyway" button (409 → override). Works end to end.
- **Video list** — grid with thumbnails, title, channel, views, relative date, pagination. 149 videos synced for CGP Grey.
- **Download pipeline** — Download (once the menu bug was fixed) runs `DownloadVideoJob` → yt-dlp with Jellyfin-style naming (`S2025E148 - …`); deno active so no "JS runtime" warnings.
- **Downloaded video playback** — the Watch page streams downloaded videos via a `<Video>` element.
- **Subscription edit page** (`/subscription/edit/{id}`) — parent folder, auto-download, max count, download order, delete-when-watched, download path, and a **Filters** section (add / preview / save). Fairly complete.
- **Folders** — create works; subscription can be assigned a parent folder.

---

## Incomplete / missing functionality (still open)

- **[MISSING] Password reset** (email flow) — logout exists, reset does not.

## Bugs / UX not yet fixed (recommend)

- **[UX/PERF] Default download quality is uncapped `bestvideo[vcodec!*=av01]+bestaudio`** (`Options.cs`). In testing it selected 4K VP9 (format 315) — a ~800MB file for one 5-minute video. Now *capable* via the Settings max-resolution option, but the shipped **default** is still uncapped; consider defaulting to `…[height<=1080]`.
- **[BUG/PERF] A 4K download stalled** — yt-dlp alive but the `.part` froze at 797MB with no progress for 15+ min (likely YouTube throttling the high itag). Live progress is now shown in the bell, but there is still **no cancel button**; `--concurrent-fragments` would also help.
- **[UX] Auto-download is ON by default** (`Subscriptions_AutoDownload = true`), bounded to the latest 3 (`MaxCount = 3`). So adding a sub silently pulls 3× videos. Should be surfaced in the add flow.
- **[PERF] Subscription sync is slow** — `FetchVideos` does a full (non-flat) yt-dlp extraction of every video, so a large channel takes minutes and no videos show until it fully completes. (Confirmed again: a CGP Grey global sync took ~4.5 min.) Consider flat listing + lazy detail.
- **[UX] After finishing the setup wizard the app briefly showed the Login screen** (token was set; a reload showed the authed app). The final auth-state change doesn't re-render into the authed view. (Observed once.)
- **[UX] Subscription/channel avatar thumbnail is broken on first paint** — loaded directly from `yt3.googleusercontent.com` and blocked by the browser (`ERR_BLOCKED_BY_ORB`) until `FetchThumbnailsJob` caches it locally. Video thumbnails (i.ytimg.com) are fine. (Distinct from the now-fixed Watch-page icon.)
</content>
