# Regard — full-feature test pass (2026-08-28)

Method: headed Chromium via Playwright driving the live app (backend :9585, frontend :5000,
`run-debug.sh`, deno on PATH), against the existing dev DB (1 sub *CGP Grey*, 149 videos, admin
account **admin / Regard!2026**). Both browser console and backend log were watched throughout.
Screenshots and probe scripts live under the job's tmp dir. DB backed up first to
`~/regard-dev-backup-goaltest-20260828-141649.tar.gz`; restored to baseline afterwards.

Legend: **[BUG]** defect · **[UX]** usability · **[ROBUST]** edge-case robustness · **[POLISH]** cosmetic.

---

## Fixed this pass (committed)

| # | What | Kind | Commit |
|---|------|------|--------|
| 1 | **Auth pages restyled** with the wizard `SetupCard` (login/register/forgot/reset). They were a bare heading + form on the plain background — `AuthLayout` copied the `.setup-card` class names but the styling is scoped to `SetupCard.razor`, so none applied. Now each page renders the real component (accent bar, card chrome, gradient submit, full-width fields); `AuthLayout` is a passthrough. | (requested) | `1ae82c9` |
| 2 | **FolderSelect lost its `<none>` (root) option after a folder-list refresh.** `Folders_DictionaryChanged` handled a `Reset` with `folders.Clear()` but never re-added the root sentinel, and `AppState.Folders.Clear()` fires exactly that Reset on every refresh. Result: a subscription/folder with no parent showed the *first real folder* as selected (a Save would have moved it there), and there was **no way to move an item back to root**. Re-add the sentinel on Reset. | [BUG] | `7b7df6d` |
| 3 | **Folder moves could form a parent cycle.** `UpdateFolder` set `ParentId` from the request with only a name check — a folder could be made its own parent, or moved under one of its own descendants, forming a loop that breaks tree rendering and the recursive folder/subscription queries. Walk the prospective parent's ancestor chain and reject if it reaches the folder being edited. | [BUG] | `5cddbbd` |
| 4 | **"1 videos (no limit)"** on the Settings storage summary — pluralize the noun by count when no quota is set. | [POLISH] | `f0ffff2` |

---

## Verified working (this pass)

- **Auth** — login/register/forgot/reset all render (now carded); the login flow signs in and lands on the app.
- **Dashboard / video list** — grid with thumbnails, title, uploader, views, relative date; watched + downloaded badges; duration badge; pagination (60/page, 149 videos → 3 pages).
- **Search** — server-side case-insensitive title filter (`VideoController.List` → `Name.Contains`). Fires on the input's `change` event (Enter/blur), not per keystroke — see F below.
- **Subscription tree / view / edit** — tree renders sub + folders; `/subscription/{id}` lists that sub's videos; the edit form carries every field, the tri-state `bool?` selects correctly show **(unset)**, and the Filters section (add / preview / save) works, including the preview modal.
- **Folders** — create appears in the sidebar immediately and defaults to root; assign a sub to a folder and move it **back to root** both persist (round-trip verified); folder edit reuses the sub config fields.
- **Settings** — Downloads + Job Log tabs; storage/quota summary; max-resolution, exclude-codecs, transcode, embed-external, subtitles, and the download filename template with a live example; Advanced expander; Save.
- **Admin** — Server tab (registration toggle, default per-user quotas, job-history retention) + Users tab; role-gated.
- **Watch (downloaded)** — the cross-origin stream plays: `GET /api/video/view` returns `200 video/mp4` with `Accept-Ranges: bytes`, range requests return `206`, and the `<video>` element reached `readyState 4` on a 4K file with no ORB error. Full metadata, linkified description, and Up-next queue render.
- **Watch (not-downloaded)** — graceful placeholder frame ("This video isn't downloaded"), Download / Watch-on-site buttons, embed-off messaging, full metadata.
- **Notifications bell** — opens; clean "Nothing happening right now." empty state.
- **Add-subscription modal** — Url + folder (defaults to `<none>`) + "Automatically download new videos" (checked by default).
- **Import modal**, **video card context menu** (correct actions per download state), **user/logout menu** — all present.
- **Download pipeline** — end-to-end verified live: yt-dlp fetch → merge → mark downloaded → streamable, on four separate videos.
- **Download cancel** — `POST /api/jobs/{id}/cancel` on a live job returns `200`, kills yt-dlp ("Invoke cancelled. Killing youtube-dl..."), sets the job to **Cancelled**, and sets the video's `DownloadSkipped = true` (verified in the DB and log). The video stays not-downloaded and is excluded from auto-download.
- **Logs** — no unhandled exceptions in the backend log across the pass, apart from the SQLite IOERR discussed in A below.

---

## Open findings (not fixed — recommendations)

### A. [ROBUST] SQLite `disk I/O error` under concurrent writes
Seen once when a download job's completion save overlapped a `video/list` read that also enriches (a
write). It surfaced as `SQLite Error 10` (`SQLITE_IOERR`, **not** `SQLITE_BUSY`, so `busy_timeout`
doesn't help) in `JobBase.PersistJobState`, plus a `500` on `video/list`. Ruled out while chasing it:
the job log is buffered in memory and persisted once at completion, and `OnJobProgress` never calls
`SaveChanges` — so this is *not* per-progress-line write amplification, just two ordinary
reader/writer connections colliding.

The DB is on **local ext4**, not a network/overlay FS — so "the filesystem can't do WAL" is *not* the
cause here. What stands out is the disk sitting at **98% full (~4.8 GB free)**: WAL grows and
auto-checkpoints, and a checkpoint/allocation stumbling on a near-full disk shows up as an I/O error.
On a healthy local disk with headroom, WAL handles concurrent readers + one writer fine and this
shouldn't occur. Switching to `journal_mode=MEMORY` would only mask it and is *less* durable — **not**
a fix, and it was reverted.

Recommendations:
1. Operational — keep disk headroom; the download target filling up is the real trigger here.
2. Optional hardening — make `journal_mode` configurable (default WAL) so a user whose DB genuinely lives
   on a filesystem that can't do WAL (a network share) has an escape hatch to `DELETE`/`TRUNCATE`. This
   is the one change that would help a class of *real* users; a healthy local-disk deployment needs nothing.
3. If it recurs on a disk with headroom, wrap the few load-bearing `SaveChanges` in a short retry — but
   don't add that speculatively; the evidence here points at disk pressure, not a code defect.

### B. [ROBUST] Downloaded-but-missing file shows a dead player
If a video is flagged downloaded but its file is gone (deleted out from under the DB), the watch page
renders a black `<video>` whose source 404s (`/api/video/view` → 404 → ORB in the console). Better to
treat "downloaded but file missing" as not-downloaded and fall back to the existing placeholder UI (which
already looks right). Edge case; low priority. (This is exactly what the stale row 149 hits in dev.)

### C. [UX] FolderEdit parent dropdown lists the folder itself (and its descendants)
Now safe — the backend guard (#3) rejects such a move with a clear message — but the dropdown still offers
those options, so the user only learns on Save. Better to exclude the edited folder and its subtree from
the `FolderSelect` on that page. Low priority.

### D. [by-design] REST `/api/jobs` always reports `cancellable: false`
That flag is populated only on the SignalR push (the bell), which is the sole surface that offers cancel.
No functional impact — noted so a future REST consumer doesn't rely on it.

### E. [UX] Search filters on Enter/blur, not as-you-type
`<input type=search @onchange>` fires on `change`. Fine for a title search; a switch to `oninput` (debounced)
would feel more live. Preference, not a defect.

### F. [POLISH] Double slash in download paths
Merged files log as `.../videos//CGP Grey/...` — `DownloadDirectory` has a trailing slash and the join adds
another. Harmless (the OS collapses it), cosmetic only.

---

## Notes
- Downloads in this environment complete in ~9 s (yt-dlp picks modest formats), so the live **cancel button**
  in the bell couldn't be caught mid-flight by UI polling; the cancel *path* is instead verified through the
  endpoint (job → Cancelled, yt-dlp killed, `DownloadSkipped` set). The bell's cancel wiring itself is
  SignalR-driven and was built + reviewed in the prior batch.
- Dev DB was restored to its pristine baseline (1 sub / 149 videos / 1 user) after the pass; test artifacts
  (extra downloads, a scratch folder, a hand-pointed `DownloadedPath`) were rolled back with it.
