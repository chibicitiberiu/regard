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

## Incomplete / missing functionality

- **[MISSING] No global Settings page.** The navbar gear links to `/settings`, but no such route exists (was the crash in #1). Server-wide/user settings (incl. download quality) have no UI.
- **[MISSING] No logout / user panel.** The navbar "person" dropdown is a placeholder literally reading *"This will be the user panel"*. There is **no way to log out** anywhere in the app.
- **[MISSING] Notifications panel is a placeholder** — dropdown content is literally *"Notifications panel"*.
- **[MISSING] Watch page for non-downloaded videos is all TODO stubs** — *"TODO: Show embedded video / View on original site / Download now / show something here"*. You cannot stream-preview, open on YouTube, or download from the watch page.
- **[MISSING] "Import (todo)"** — Add-menu item labeled unfinished.
- **[MISSING] Download quality/format is not configurable in the UI.** The per-subscription edit page exposes auto-download/count/order/path/filters but **not** `Ytdl:Format`. Combined with the default below, every download is max quality with no user control.

## Bugs / UX not yet fixed (recommend)

- **[UX/PERF] Default download quality is uncapped `bestvideo[vcodec!*=av01]+bestaudio`** (`Options.cs:237`). In testing it selected 4K VP9 (format 315) — a ~800MB file for one 5-minute video. Recommend a sane default cap (e.g. `…[height<=1080]`).
- **[BUG/PERF] A 4K download stalled** — yt-dlp alive but the `.part` froze at 797MB with no progress for 15+ min (likely YouTube throttling the high itag). With no progress UI and no cancel, it hangs silently. A quality cap and/or `--concurrent-fragments` would help.
- **[UX] Auto-download is ON by default** (`Subscriptions_AutoDownload = true`), bounded to the latest 3 (`MaxCount = 3`). So adding a sub silently pulls 3× max-quality videos. Reasonable to keep, but should be surfaced/configurable in the add flow.
- **[UX] No download progress indication** anywhere in the UI after clicking Download; the video only flips to "downloaded" when done.
- **[PERF] Subscription sync is slow** — `FetchVideos` does a full (non-flat) yt-dlp extraction of every video, so a large channel takes minutes and no videos show until it fully completes. Consider flat listing + lazy detail.
- **[BUG] Watch page hardcoded placeholder channel icon** — `<img src="https://i.imgur.com/7DXZees.jpg">` (`Watch.razor`).
- **[UX] Empty state is a blank void** — with no subscriptions the whole screen is empty with no "add your first subscription" guidance.
- **[UX] Modals don't close on `Escape`.**
- **[UX] After finishing the setup wizard the app briefly showed the Login screen** (token was set; a reload showed the authed app). The final auth-state change doesn't re-render into the authed view. (Observed once.)
- **[UX] Subscription/channel avatar thumbnail is broken on first paint** — loaded directly from `yt3.googleusercontent.com` and blocked by the browser (`ERR_BLOCKED_BY_ORB`) until `FetchThumbnailsJob` caches it locally. Video thumbnails (i.ytimg.com) are fine.
</content>
