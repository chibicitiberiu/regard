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

## Follow-up fixes (open items addressed)

Revisited under the "investigate still open issues" pass and fixed:

| Item | Fix | Kind | Commit |
|------|-----|------|--------|
| **B — downloaded-but-missing file showed a dead player** | The watch page tracks a `streamFailed` flag on the media error and falls through to the placeholder with a tailored *"This video couldn't be played… Download it again, or watch it on the original site"* and a **Download again** action, instead of a black frame. Verified against a video flagged downloaded with a 404 stream. | [ROBUST] | `56d2f49` |
| **C — FolderEdit offered the folder itself as a parent** | `FolderSelect` gained `ExcludeSubtreeRootId`; FolderEdit passes its own id, so the folder and its whole subtree drop out of the list (backend guard #3 still enforces it). Verified: `/folder/edit/1` now lists only `<none>`. | [UX] | `994e946` |
| **F — double slash in the resolved download path** | Collapse runs of the path separator left by an empty `{FolderPath}` (root-level sub), keeping a single leading separator so an absolute path stays absolute. | [POLISH] | `897f4c2` |
| **A — SQLite journal mode not tunable** | `journal_mode` is now overridable via `REGARD_SQLITE_JOURNAL_MODE` (WAL/DELETE/TRUNCATE/PERSIST/MEMORY/OFF); default and any invalid value stay **WAL**, so a local-disk deployment is unchanged. Gives the one class of real users who need it — a DB on a filesystem that can't do WAL — an escape hatch. Verified `=DELETE` switches the mode and the app runs. | [ROBUST] | `570f15a` |

On **A**, the trigger here remains **disk pressure** (the machine's disk sits at ~98% full): on a healthy
local disk WAL handles concurrent readers + one writer fine, and this shouldn't occur. The env override is
a hardening for the network-share case, **not** a substitute for disk headroom, and `MEMORY` was never
shipped. Chasing it also ruled out write amplification — the job log is buffered and persisted once, and
`OnJobProgress` never calls `SaveChanges`.

## Still open (intentionally not changed)

### D. [by-design] REST `/api/jobs` always reports `cancellable: false`
That flag is populated only on the SignalR push (the bell), which is the sole surface that offers cancel.
No functional impact — noted so a future REST consumer doesn't rely on it.

### E. [UX] Search filters on Enter/blur, not as-you-type
`<input type=search @onchange>` fires on `change`. Fine for a title search; a switch to `oninput` (debounced)
would feel more live. Preference, not a defect — left as is.

---

## Notes
- Downloads in this environment complete in ~9 s (yt-dlp picks modest formats), so the live **cancel button**
  in the bell couldn't be caught mid-flight by UI polling; the cancel *path* is instead verified through the
  endpoint (job → Cancelled, yt-dlp killed, `DownloadSkipped` set). The bell's cancel wiring itself is
  SignalR-driven and was built + reviewed in the prior batch.
- Dev DB was restored to its pristine baseline (1 sub / 149 videos / 1 user) after the pass; test artifacts
  (extra downloads, a scratch folder, a hand-pointed `DownloadedPath`) were rolled back with it.
