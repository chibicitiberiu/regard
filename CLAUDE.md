# Regard — project notes for Claude

C# YouTube subscription manager (a revived ytsm). .NET 10, ASP.NET Core backend + Blazor WebAssembly
frontend, EF Core over SQLite (dev) or SQL Server (prod), Quartz for jobs, yt-dlp as a child process.

My global `~/.claude/CLAUDE.md` still applies (git rules, no sudo, back-ups before destructive ops,
writing style, model routing, delegating exploration). This file is the Regard-specific layer on top.

## Running it locally

`./run-debug.sh` drives everything (SQLite, no Docker). Data + downloads live in `.dev/` (gitignored).

- `./run-debug.sh backend` → API on http://localhost:9585
- `./run-debug.sh frontend` → Blazor dev server on http://localhost:5000
- `./run-debug.sh both`

Env the script sets: `DataDirectory=.dev/data`, `DownloadDirectory=.dev/videos`, `REGARD_MIGRATE=1`
(schema upgrades on start), `ASPNETCORE_ENVIRONMENT=Development`. Parallel job count is
`REGARD_MAX_PARALLEL_JOBS` (default 3). DB file is `.dev/data/Regard.db`.

When you start the backend/frontend for testing, use a **`run_in_background` tool call**. A plain `&` or
`nohup … &` inside a normal Bash call dies the moment the tool call returns — that wasted a couple of
rounds. Before starting one, confirm `:9585` (or `:5000`) is actually free; a leftover instance silently
makes the new one fail to bind. **In a backgrounded command, don't `cd` — call the script by absolute
path** (`/home/tibi/Dev/regard/run-debug.sh backend > /some/abs.log 2>&1`); a `cd` in a compound
background command gets permission-gated and the whole task fails, exit 1, with no log. `run-debug.sh`
resolves its own repo root, so an absolute path works from any cwd.

To stop instances: `pkill -9 -f "Regard.Backend"` (and `Regard.Frontend` / `blazor-devserver`). Use
`-9`, not a graceful `-TERM` — a graceful stop with `WaitForJobsToComplete=true` **hangs** while a
download is active, and you end up with orphaned backends polluting the DB. SQLite is WAL, so a hard kill
is crash-safe.

## Database & migrations — TWO contexts

There are two `DbContext`s and every schema change needs a migration for **both**, or SQL Server breaks
in prod while SQLite looks fine:

- `SQLiteDataContext`  → `Source/Regard.Backend/Migrations/SQLite/`
- `SQLServerDataContext` → `Source/Regard.Backend/Migrations/SqlServer/`

`dotnet-ef` is a **local** tool (`Source/dotnet-tools.json`), so invoke it as `dotnet dotnet-ef`:

```
dotnet dotnet-ef migrations add <Name> -c SQLiteDataContext   -o Migrations/SQLite   --project Source/Regard.Backend
dotnet dotnet-ef migrations add <Name> -c SQLServerDataContext -o Migrations/SqlServer --project Source/Regard.Backend
```

Back up `.dev/data/Regard.db` (timestamped copy) before a migration or any bulk data op — yes, even
"safe" ones.

### SQLite query gotchas that have bitten us
- **`DateTimeOffset` isn't translatable** in SQLite LINQ (ordering/`<`/`>` comparisons throw). Filter on
  what the provider *can* translate server-side, then `.AsEnumerable().Where(...)` the date comparison in
  memory. See the deletion sweep and `PruneOldJobs` for the pattern.
- **Don't trust the `sqlite3` CLI against the live DB.** The app holds the WAL connection, so the CLI
  reads only the committed main file and lags behind — it showed me an *empty* Notifications table while
  the app had three live rows. Read live state through the API (`api.py`), not `sqlite3` on `Regard.db`.
- The notification row's timestamp column is **`Timestamp`**, not `DateTime`. A wrong column name makes
  `sqlite3` print to stderr and return empty stdout, which looks exactly like "no rows" — double-check
  the schema before concluding data is missing.
- **The running backend does not see an external write to the live DB.** The reverse of the point above,
  and it bit me on 2026-08-30: a test flipped `Videos.SponsorsRemoved` with the `sqlite3` CLI, asserted,
  then flipped it back. The file was correct afterwards (CLI read 0, no `-wal` left), but the API kept
  serving the old value on every request until the backend was restarted. So a test that mutates the DB
  underneath a running app must **restart the backend before verifying the restore** — otherwise
  "restored" is unproven, and you can't tell a stale cache from a failed write.
- Disk near-full (~96%+) → SQLite **Error 10 (disk I/O)**. That's a real error and is correctly *not*
  retried, but it can wedge the connection until a backend restart even after you free space. `SQLITE_BUSY`
  (error 5/6) is the one we retry (`busy_timeout=30000` + a small retry loop in `SQLiteDataContext`).
  **A full disk is not the only trigger, and it does not always wedge.** Seen twice on 2026-08-31 at 92%
  used with 19 GB free, both times during heavy concurrent job activity (several jobs plus yt-dlp
  processes), after a session of repeated `pkill -9` and `sqlite3` writes against the live file:
  - **Wedged:** every query failed afterwards, `POST /api/auth/login` included, so unrelated endpoints
    returned a bare 500 that looked exactly like a bug in the code just written. Only a restart cleared it.
  - **Self-healed:** a burst of ~12 errors across a couple of minutes, then normal service — a sync
    completed and writes succeeded with no restart.

  So: check the log for `SQLite Error 10` before debugging your own 500, and don't assume a restart is
  required — verify with an actual write first. `pragma integrity_check` returned `ok` both times and no
  rows were lost.

  **Fourth occurrence 2026-09-03**, at 94% used / 15 GB free, during a long test session (repeated
  `pkill -9`, several restarts, many jobs). Same shape: wedged, every request 500 including login,
  `integrity_check` **and** `quick_check` both `ok`, all 508 videos / 4 subs / 1 user present, no kernel
  I/O errors, inodes 16%. One restart fixed it. Worth recording because it happened minutes after a
  successful in-place `VACUUM` and it would be easy to blame that: the vacuum completed, the file
  shrank, and reads and writes were verified working immediately afterwards, with several test suites
  passing in between. The trigger looks like the same disk-occupancy-plus-activity condition as the
  other three, not the vacuum.

  **It also comes as `SQLite Error 11: 'database disk image is malformed'`** — same conditions, same
  outcome, scarier wording. Third occurrence on 2026-08-31, again at 92% used / 19 GB free, again during
  concurrent job activity. Eight errors in a burst, every request 500ing afterwards until a restart, and
  the message names the one thing that was *not* wrong: `integrity_check` **and** `quick_check` both
  returned `ok`, all 508 videos / 4 subs / 1 user present, no kernel I/O or ext4 errors in `dmesg` or
  `journalctl -k`, inodes 15% used. **Do not act on the word "malformed" — check integrity first, then
  restart.** A restore from backup would have been destructive here for no reason.

## Logging

NLog, configured in **three** files that must stay in step: `Source/Regard.Backend/nlog.config` (local
runs), `Docker/Backend/nlog-Release.config` (**what the image actually ships** — `Dockerfile` copies it
to `nlog.config`), and `Docker/Backend/nlog-Debug.config` (referenced by nothing today).
`NLogConfigTests` loads all three for real, which is the only thing that does: CI builds and pushes the
image but never *loads* the config, and with `throwConfigExceptions="true"` plus `Program.cs` calling
`SetupLogger()` outside its `try`, a typo is an unhandled startup crash with no log output.

- Output is `{DataDirectory}/Logs/regard-<date>.log`, a fixed **8 pipe-delimited fields**:
  `longdate|eventId|LEVEL|logger|callsite|request-url|mvc-action|message + exception`. Empty fields on
  job-originated entries are deliberate — the shape is regular so it can be parsed.
- **NLog 6 archive idiom is the opposite of what the old config did.** `archiveNumbering` and
  `archiveFileName` are marked legacy, and both `archiveEvery` and `archiveFileName` only work when
  `fileName` is *static* — ours carries `${shortdate}`. So: keep the dynamic `fileName`, delete those
  two, keep `archiveAboveSize`, add `maxArchiveDays`. Verified: back-dated files past the window are
  removed at the next new-file open (each date rollover, and each restart).
- `maxArchiveFiles` and `maxArchiveDays` are independent OR'd conditions but compose as a *minimum*, so
  a low file count silently caps the day retention. `maxArchiveFiles="0"` is not "unlimited" — it
  selects a handler that truncates the active log.
- **`maxArchiveDays` is a plain `int`, not a `Layout<int>`** — `${environment:...}` there is parsed as
  an int, fails, and kills the process at config load.
- `internalLogLevel` without `internalLogToConsole`/`internalLogFile` writes nowhere, which is why the
  obsolete attributes above never announced themselves for as long as they were wrong.
- `Logs/ytdl/*.txt` (one file per yt-dlp invocation) is **not** NLog's, so its retention never sees
  them; `MaintenanceJob` prunes them **by mtime**. Do not parse those names, and **do not sort by them
  either**: they carry a 12-hour clock with AM/PM glued on the end, so `12…AM` sorts *after* `01…PM`
  while being eleven hours earlier. They are only written when `configuration["Debug"]` is true, i.e.
  development only — the shipped container writes none.

### Reading the logs back (Batch 6b)

`LogLineParser` turns the files into entries for the admin log viewer. Three properties of the real
files drive it, each measured rather than assumed:

- **Two layouts coexist, sometimes in one file.** Batch 6a widened the layout from 5 fields to 8
  mid-day, so `regard-2026-09-03.log` holds both. Detection is **per line**, by field 5: a `${callsite}`
  has no whitespace and always contains a dot (`Namespace.Type.Method`); a message is prose. The dot
  matters — a message with 3+ pipes fills all eight slots of the bounded split, so field count alone
  cannot decide. Zero misreads across 69,815 old-format lines.
- **Messages contain pipes** (a video titled `Swimming | Episode 2`; 681 such lines in one file), so
  every split takes a limit and the message keeps its own separators.
- **~9% of lines are continuations** — stack traces with no leading timestamp, belonging to the entry
  above. Treating lines as entries turns one failure into a dozen rows.

Other things that are load-bearing there:
- **Open the live file `FileShare.ReadWrite | FileShare.Delete`.** NLog holds the current day's file
  open for writing; `Delete` matters too, or a read in flight blocks NLog's retention from rolling an
  aged-out file away.
- **Never `Path.Combine` a client-supplied file name.** `LogFileReader` resolves a requested name by
  looking it up in the directory listing, so a traversal is simply not in the list — including
  `../regard-<date>.log`, which stays inside `Logs/` and would satisfy a naive prefix check. Same rule
  as `VideoController.Subtitle`.
- **Downloads use an authenticated `fetch`** (`RegardHelpers.downloadWithAuth`), not an `<a href>`.
  A bare link sends no `Authorization` header, which is the *only* reason `/api/video/view` and
  `/api/video/subtitle` are in `QueryStringAuthMiddleware`'s whitelist; don't widen it for convenience.
- Verification that actually catches a broken parser: **reconcile the entry count per file** against an
  independent count of timestamp-prefixed lines. Everything else (it renders, levels look right) passes
  even when entries are being silently dropped or invented.

## Frontend / styling

**Edit only `.scss`.** `style.css` (+ `.map`) is generated and gitignored; `AspNetCore.SassCompiler`
recompiles it on build. Source of truth is `wwwroot/css/style.scss` and `wwwroot/css/lib/_*.scss`.

**Every `<button>` is styled as a solid chip, and its hover inverts the text.** `_controls.scss:76`
matches bare `button`, and `button:hover:not(:disabled)` sets `color: var(--color-bg-secondary)`,
`background: var(--color-fg-secondary)` and a glow. Two consequences for any button that isn't a chip
(list rows, icon buttons, anything overlaying the player):

- **Restate `color` and `box-shadow`, not just `background`.** A rule that only sets the hover
  background inherits the base rule's inverted text. Where the element tints itself with
  `--color-bg-secondary` — which the chapter and SponsorBlock rows do — the label becomes exactly the
  colour of its own background and *vanishes on hover*. That shipped unnoticed in the chapters list
  from Batch 4b until 2026-08-31.
- **Watch the specificity.** `button:hover:not(:disabled)` is **(0,2,1)**, so a plain
  `.my-button:hover` at (0,2,0) **loses** and your whole hover rule is silently discarded. Needs a
  second class, a descendant selector, or an attribute `:not()` to outweigh it —
  `.watch-chapter-btn:hover:not([disabled])` is (0,3,0) and wins.

Known still-affected: `.watch-btn:hover` (`_watch.scss`) is (0,2,0), so the action-button row's intended
`--color-bg-hover` never applies and it takes the inverted chip instead. Legible, so left alone.

Assert hover legibility on a **computed contrast ratio**, never on a colour literal — the palette is
themed and a literal locks in one theme while letting the bug back in on another.

**The theme's semantic colour pairs are not usable for small text.** `--color-fg-info` on
`--color-bg-info` measures about **2.3:1**, and the same holds for the warning pair; the existing
`.job-state-*` chips use them. For anything pill-sized, put the semantic *foreground* colour on
`--color-bg-secondary` instead — that measures 9-14:1 and keeps the colour coding. The log-level pills
in `_logs.scss` do this (errors keep a solid `--color-bg-error` chip with white text, 5.3:1, because
they should be loud).

**`table-layout: fixed` takes its column widths from the first row**, so widths belong in a
`<colgroup>`, not on the `<td>`s. Set them on cells and the browser gives every column that didn't
declare one an equal share of the remainder — which is how the log viewer's message column ended up a
quarter of the table while Time and Level sat on slack.

## Testing (how I like it verified)

I ask for changes to be **implemented and tested, usually with Playwright**, not just compiled.

- Playwright with `headless=False` under `DISPLAY=:0`, interpreter `/usr/bin/python3.14`.
- `wait_until="domcontentloaded"` (the app is a SPA; `networkidle` stalls).
- Auth by injecting the token into `localStorage` (`authToken`) via an init script; creds `admin` /
  `Regard!2026`.
- There's an `api.py` helper for driving the backend directly. Real endpoints: login is
  `POST /api/auth/login`; notifications the bell reads are `GET /api/notifications/recent?take=N`
  (not `/list`). When in doubt, read live state through the API rather than the DB file.
- **Enums bind as ints, not names.** `subscription/edit` rejects `"downloadOrder": "Oldest"` with a 400;
  send `1`. Same for `ApiSubscription.Parts` (`Config` = `1`), which is a flags int rather than an array.
  A test helper that ignores the response makes this look like a product bug — check for `httpError`.
- **`Subscriptions_MaxCount` is a cap on the total kept, not on one run.** `DetermineMaximumVideoCount`
  subtracts what's already downloaded, so setting it to 1 on a subscription that already has 1 file
  yields zero download slots and nothing is ever queued.
- **A sync's video count plateaus between tabs.** A flat channel listing drains Videos, then Live, then
  Shorts, so polling for a "stable" count reports done early. Wait for the `SynchronizeJob` row to reach
  a terminal `state` (3/4/5) instead.
- **Cancelling a download sets `DownloadSkipped`** (`JobsController`), so a test that cancels to avoid
  pulling a real file has to clear the flag afterwards. `DownloadSkipped` is not on `ApiVideo`, so read
  it from a snapshot of `Regard.db` + `-wal` + `-shm` copied together.
- **`subscription/delete` with `deleteDownloadedFiles: true` is asynchronous** — it queues a file-sweep
  job, and the rows are still there right after the call returns. Pass `false` for a subscription with
  no downloads if you want the delete to be immediate.

## Architecture cheat-sheet

- **Jobs**: Quartz, **in-memory** trigger store. `JobBase.Execute` is the lifecycle (`ShouldDefer` →
  `OnJobStarted` → `ExecuteJob` → `OnJobCompleted`/`Failed`/`Cancelled` → `finally` runs `OnAfterExecute`
  + `PersistJobState`). Because the store is in-memory, a **backend restart drops every trigger**, leaving
  the job's `JobInfo` row stranded at `Scheduled`/`Running`. On boot `InitJob` runs a **reconciliation
  sweep** (`JobTrackerService.GetOrphanedJobs` → per row `RegardScheduler.TryResume` else
  `AbandonJob`): job types marked `[ResumeAfterRestart]` (downloads, imports, the DeleteFiles family,
  DeleteUser) are re-enqueued from their persisted row (all payload lives in `JobInfo.JobDataJson`, so the
  DB row is enough); everything else — the recurring/maintenance jobs re-scheduled fresh each boot (sync,
  thumbnails, deletions sweep, ytdl update, Jellyfin) — is marked `Cancelled` "Interrupted by server
  restart". Legacy rows with an empty `Key` (pre-dating the Key-persistence fix) can't be rebuilt, so they
  abandon too. To opt a new job type into resume, add the attribute; the default (absence) is abandon.
- **JobTrackerService** persistence trap: its `OnJob*` handlers open a *fresh* scope that does **not**
  track the `job` object, so a plain `SaveChanges()` writes nothing. Persist via `dataContext.Update(job)`
  first (see `OnJobScheduled`/`OnJobStarted`) or rely on `JobBase.PersistJobState`. This is why
  `JobInfo.Key`/`State` silently failed to save (Job Log showing "Scheduled" for a running job) and
  download retries were dead ("Name cannot be null").
- **`JobInfo.Progress` and `Detail` are `[NotMapped]`** — they never hit the Jobs table, and `Log` is
  only written at completion. So a polling reader (the Job Log) can't see a running job's progress/step/
  log from the DB. Live values live in `JobTrackerService.liveJobs` (in-memory, keyed by job id, cleared
  on terminal state); `JobsController.ToApi`/`GetOne` overlay them for `Running` jobs. `State`, `NextRun`,
  `Started`, `Completed` *are* real columns.
- **Notifications**: `NotificationService` (singleton), one row keyed by `(UserId, Key)`, pushed over
  SignalR. Download jobs key ongoing cards by `job:{id}`; the throttle "Queued for download" card is keyed
  by `download:{videoId}` so it survives the reschedule cycle. `PostOrUpdate` writes to the DB **and**
  sends; it swallows its own exceptions and logs "Failed to persist notification".
- **Throttling / anti-bot** (`HostThrottle`, `YtdlAntibotArgs`): per-hosting-domain concurrency + jittered
  pacing + caps, keyed on the URL host (`UrlHostKey.Of`). It **reschedules** a job (`StartAt`) instead of
  parking a worker. yt-dlp anti-bot args are built **per call** (never a shared `BaseArguments` field —
  that races cookies across hosts at pool > 1). Cookies live at `<DataDirectory>/cookies.txt`, uploaded
  from the admin page.
- **yt-dlp REWRITES whatever `--cookies` points at.** Confirmed on 2026-08-30: after a run, the file
  begins `# This file is generated by yt-dlp. Do not edit.` and carries the session's cookies. Two
  consequences. (1) The cookies path must never be settable by a user — that would be an arbitrary file
  *overwrite*, the database included; `UserCookiesService` derives it from the account id and the
  endpoint takes content only. (2) Deleting a user's jar races any in-flight extraction, which can
  recreate the file, so "is it configured?" is decided by the stored option, not by `File.Exists`.
- **`--impersonate` is guarded by a probe, and must stay that way.** yt-dlp raises in
  `YoutubeDL.__init__` when the target can't be resolved, so an unavailable target doesn't disable
  impersonation — it fails *every* extraction and download before any network call. `--impersonate=`
  ("any") fails identically. `YoutubeDLService` therefore runs `--list-impersonate-targets` once per
  yt-dlp version and `YtdlAntibotArgs.ResolveImpersonate` drops the flag (with a warning) unless the
  configured client is in that list. Availability comes from **curl_cffi in the Python running the
  zipapp**, not from yt-dlp itself, so it differs between the Docker image (has it) and a bare host.
- **Subtitles** (Batch 4b). Sidecars land as `<prefix>.<lang>.vtt`, except when SponsorBlock *remove* is
  on — `DownloadVideoJob` then forces `--convert-subs srt` so yt-dlp can re-time the cues to the cut
  file. What follows from that:
  - `MimeMapping` knows `text/vtt` but has **no `srt` entry**, so a naive `GetMimeMapping` would serve
    SubRip as `application/octet-stream`. `VideoController.Subtitle` states `text/vtt` outright and
    converts. It also strips per-cue `align`/`position` settings, because YouTube's ASR files pin every
    cue to the left edge and CSS **cannot** fix that — alignment is a cue setting, not a style.
  - A `<track>` sends no `Authorization` header, so `/api/video/subtitle` is in
    `QueryStringAuthMiddleware.WhitelistedPaths` next to `/api/video/view`, and the `<video>` carries
    `crossorigin="anonymous"` (a cross-origin text track is otherwise not exposed to the page, and in
    dev the API is `:9585` while the UI is `:5000`). Adding `crossorigin` makes the *video stream* a CORS
    request too — that was verified not to regress, and there's a test guarding it.
  - Every language is mounted as a `<track>` up front. That is not a download: a track whose `mode` is
    `disabled` is never fetched, so the cues load only when one is switched on.
  - `TextTrack` exposes the `srclang` attribute as **`.language`**; matching on `.srclang` silently
    matches nothing, and the symptom is a track that loads but never displays.
  - **Where Chrome puts its captions button varies by build, so don't assert either way.** Batch 4b
    measured it inside the `⋮` overflow menu (with Download and Picture-in-picture) and added an overlay
    button of our own on that basis; Batch 5b then saw a real browser render one *inline in the control
    bar*, giving two CC buttons a few pixels apart. Playwright's bundled Chromium still uses the overflow
    menu at every width from 636 px to 1032 px, so a test cannot tell you what a user sees.
    **Our overlay is gone** — the native control lists our labels, because they come from the `<track
    label>` attribute — and the trade is that in a build which hides captions in `⋮`, they are one extra
    click away. The `change` listener is kept regardless: it is what remembers the chosen language.
- **Sidecar-only reprocess** (Batch 5b). `ReprocessVideoJob` fetches subtitles for an already-downloaded
  video with `--skip-download`, and reads back the info-json the same run writes so one extraction also
  refreshes the metadata. Four things about it are load-bearing:
  - **`--ignore-errors` is not optional.** yt-dlp writes subtitles *before* the info-json
    (`YoutubeDL.process_info`), and its `_write_subtitles` raises `DownloadError` on a per-language
    failure unless errors are ignored — so a single 429 on the caption endpoint (common) aborts the
    video and loses both the metadata and the languages that had already been written.
  - **The exit code is ignored; the files on disk are the answer.** A partial fetch is the normal case,
    and `SubtitleNeeds` treats it as still-incomplete so a retry gets the rest.
  - **Never reuse `DownloadVideoJob.ProcessStdout`.** yt-dlp prints `[download] Destination: …en.vtt`
    for every subtitle, and that handler treats a `Destination:` line as the media file — it would
    rewrite `Video.DownloadedPath` to `<title>.en`. Confirmed against live output.
  - **`-o` gets `Video.DownloadedPath` verbatim**, never a freshly-resolved output path: if the
    subscription was renamed, the template renders a different prefix and the sidecars land where
    subtitle discovery will never look.
  - Also: `--sub-format` is forced to `vtt/srt/best`, because the stored default `best` can resolve to
    `json3`, which `SubtitleFile` doesn't recognise — the file would exist but be invisible, and the
    sweep would re-fetch it forever. And `SponsorsRemoved` videos are refused outright: the media is
    already cut, so fresh cues would never line up.
- **"Fetch subtitles" and "Refresh metadata" are separate actions on purpose.** The reprocess job
  refreshes metadata only as a by-product of actually fetching something, and returns before yt-dlp runs
  at all when the video's subtitles are already complete (the common case) — so it is not a way to
  refresh a stale view count. `RefreshVideoMetadataJob` is, and unlike the background sweep it ignores
  the age-based schedule and does not defer for downloads, because a person is waiting on it.
- **A cut file's SponsorBlock data is snapshotted, never re-fetched** (Batch 5b).
  `Video.SponsorSegmentsRemoved` records what `--sponsorblock-remove` actually cut, captured at download
  time. SponsorBlock is crowd-sourced and keeps moving, so a later fetch describes a different cut than
  the bytes on disk; for a cut video the snapshot is the only correct version. It is also the missing
  piece that would let subtitles, `Chapters` and description timestamps be mapped onto a cut file —
  all three are disabled for cut videos today precisely because this was never recorded.
- **`Sponsorblock_Actions` ships non-empty, so "off" is the literal `none`** (Batch 5d). The default is
  `sponsor:skip` — SponsorBlock's own shipped default, which is narrower than people expect (their
  `categorySelections` is sponsor AutoSkip, poi_highlight ManualSkip, exclusive_access/chapter
  ShowOverlay; every other category is off). An empty/null option value means *unset* and resolves back
  to that default, so `SponsorBlockActions.Serialize` emits `none` for an all-Keep map — otherwise
  turning everything off in the UI silently re-enables the default. Subscription-level inherit is
  therefore no longer expressible as "all Keep" and needs `SponsorBlockEditor AllowInherit="true"`.
- **The watch page fetches every SponsorBlock category, not just the configured ones** (Batch 5d), and
  marks each `ApiSponsorSegment.Skip` from the config. That's what lets the segment panel offer an intro
  or outro to skip for one video without touching settings. One request either way. It is still gated
  on at least one configured skip category, so a user who turns SponsorBlock off generates no traffic to
  sponsor.ajay.app at all.
  - `Skip` is *mutable on the client*: the panel's checkboxes flip it and call
    `Video.RefreshSkipSegments()`, which re-arms the JS handler with the enabled subset. Indices into
    `ApiVideo.SponsorSegments` are the identity used by the skip callback, so that list must stay stable.
  - **Order of operations for Undo — disarm, re-arm, then seek.** Seeking back into a segment that is
    still armed is instantly undone by the next `timeupdate`.
  - **A fullscreened `<video>` hides its siblings**, so any overlay of ours dies in fullscreen.
    `RegardHelpers.addFullscreenPromotion` watches `fullscreenchange`, and when the browser fullscreens
    the video element it hands fullscreen to `.watch-player` instead. Needs transient activation, which
    the click that opened fullscreen still provides; if it fails, you just get the old behaviour.
  - **Testing: never assert a skip on a fixed sleep.** The stream is served over range requests, so a
    seek's `seeked` — and the `timeupdate` the skip rides on — can land seconds later. Wait for the
    toast. Escape can't be tested either: exiting fullscreen on Escape is browser chrome, not a DOM
    event, so use `document.exitFullscreen()`.
- **Background metadata refresh is the lowest-priority thing in the system** (Batch 5b).
  `RefreshMetadataJob` stands down whenever `HostThrottle.HasDownloadPressure` reports a download in
  flight or queued, or a `SynchronizeJob` row is `Running` — checked in `ShouldDefer` and again between
  videos. That matters because extractions and downloads share the per-host `NextAllowedUtc`, so an
  unchecked refresh pass pushes waiting downloads out indefinitely, and the hour/day caps would never
  notice: **they count downloads only** (`TryReserveDownload` is the sole writer to `DownloadTimes`).
  - Each video gets its own interval from its age (`RefreshSchedule`): 1 day under a week, out to 90
    days past a year. Un-enriched videos are excluded — their `Published` is a `MinValue` sort
    placeholder, so the curve says nothing about them.
  - `Video.Rating` is backfilled from Return YouTube Dislike, which is a different host on a different
    budget (100/min) and outside `HostThrottle` entirely. Store
    `ProviderHelpers.CalculateRating(likes, dislikes)`, **never** `votes.Rating` — that one is YouTube's
    legacy 1..5 star average, while `Video.Rating` is a 0..1 ratio the watch page multiplies by 5.
- **`video.js` is loaded but deliberately not initialised.** Its `autoSetup` re-polls every 1 ms until
  `window load` and wraps any element carrying `data-setup`, so that attribute was removed from
  `Video.razor` — otherwise whether we get a native `<video>` or a video.js player depends on boot
  timing. Everything in `RegardHelpers` drives the raw DOM element.
- **Providers** are a separate project and can't touch options/`HostThrottle` directly — they go through
  `IYoutubeDlService` (`GetAntibotArgs`, `PaceExtractionAsync`). That's why the content-scope filters
  live in `SynchronizeJob` rather than in `YouTubeDLProvider`.
- **`IProviderManager.FindForVideo` costs a full yt-dlp extraction per call.** It probes every provider
  with `IVideoProvider.CanHandleVideo`, and `YouTubeDLProvider` answers that by actually running
  `--dump-single-json` (paced). In a loop that silently doubles the request count — measured as two
  identical invocations per video before it was spotted. Use
  `providerManager.Get<IVideoProvider>(video.VideoProviderId)`, a dictionary lookup; every video carries
  a provider id (`"YtDL"`). `VideoManager.EnsureEnriched` still takes the expensive path, which is
  tolerable for one video but would not be for a batch.
- **Content scope** (Batch 5a). "Include Shorts" / "include members-only" are decided during sync, before
  the row is created, so an excluded video is never stored and an already-stored one is never touched.
  - **A channel subscription never sees a Short on its own.** `YouTubeUrlHelper.FixYouTubeChannelUri`
    rewrites `youtube.com/@Handle` (and `/channel/ID`, `/c/`, `/user/`) to that channel's **`/videos`
    tab** at creation time, and YouTube's Videos tab excludes Shorts — they have their own tab. So with
    the option on, `CheckForNewVideos` lists the sibling `/shorts` URL as a second pass. A
    `/@Handle/shorts` URL has three path segments and escapes the rewrite, so subscribing to it directly
    works too.
  - Both signals are present in a plain `--flat-playlist` listing: a Short's `url` is
    `youtube.com/shorts/<id>`, and a members-only video reports `availability: subscriber_only` (yt-dlp
    reads it off the channel page's badge, so **no cookies and no membership are needed to detect it**).
    `UrlInformation.Availability` carries it, and `Video.ProviderAvailability` is `[NotMapped]` — a
    sync-only hint, always null on a video loaded from the database.
  - **Don't use duration to detect a Short.** Videos 187 and 193 in the dev library are 32 s and 9 s and
    are ordinary CGP Grey uploads; a length threshold eats them.
- **The publish-date window is checked twice, on purpose.** Un-enriched videos carry
  `Published = DateTimeOffset.MinValue` (a sort placeholder set at `SynchronizeJob`), so testing them in
  `ProcessDownloadRules` would exclude every flat video forever — nothing would ever enrich them again.
  Stage 1 skips un-enriched videos; `DownloadVideoJob` re-checks right after `EnsureEnriched` and marks
  an out-of-window video `DownloadSkipped`. Only automatic downloads are gated: `ProcessDownloadRules`
  passes an `Auto` job-data flag (alongside `Forced`), because a plain manual download is unforced too
  and must still win over the filter.
- **Options**: `OptionDefinition<T>(default, key, configKey, envKey, flags)`; `flags = 0` means
  server-only. Read with `optionManager.GetGlobal` (DB → env → config → default). Nothing hardcoded —
  defaults are option defaults, admin-tunable.
  - **There is no `UnsetGlobal`** — only `SetGlobal`, plus the per-user/subscription/folder unsets. The
    "empty means default" convention at global level is a *consumer* convention:
    `SetGlobal(Options.X, request.X ?? "")`, as `Server_Ytdl_LimitRate` does.
  - **A null `Key` is a landmine.** `GetFromDatabase`/`GetFromEnvironment`/`GetFromConfiguration` all
    guard null, but `GetGlobal` hits the cache *first* and that is a `Dictionary`, so a definition with
    `Key = null` throws `ArgumentNullException` the moment anyone passes it to `GetGlobal`. If an option
    must not be database-backed, use plain constants instead (see `Server_Backup_PreMigration_*`).
  - **Anything read before `Database.Migrate()` cannot be an option at all.** `GetFromDatabase` queries
    the `Options` table, which does not exist on a first run.
- **Backups** (Batch 6a). `DatabaseBackupService` snapshots with SQLite's `VACUUM INTO`: a read
  transaction against the live database, so nothing stops, no writer blocks, the copy is compacted, and
  it is self-contained — no `-wal`/`-shm` sidecar. SQLite only; on SQL Server every operation reports
  itself skipped, because `BACKUP DATABASE` writes on the *database* host, not this filesystem.
  - **`VACUUM INTO` is not atomic and does not clean up after a partial failure.** Write `.db.tmp`,
    `PRAGMA integrity_check` it, then `File.Move`. Skip that and a backup killed by a full disk leaves a
    truncated file that is the newest match, which retention counts as good and swaps for a real one.
  - **`ExecuteSqlRaw("VACUUM INTO {0}", path)`** — the parameter binds and is evaluated at run time.
    Also assert `Database.CurrentTransaction` is null; SQLite refuses `VACUUM` inside a transaction.
  - **The destination is derived (`StorageManager.BackupDirectory`), never settable.** Retention deletes
    files there, so a configurable path would be an arbitrary-deletion primitive aimed at whatever an
    admin typed — and the obvious thing to type is the data directory. Same reasoning as the cookies
    path. Like it, this directory must never be served: a snapshot holds every password hash.
  - **Retention only ever matches names it could have written** (prefix + exact UTC stamp + `.db`), so
    hand-made copies in the same directory survive. UTC because a local timestamp repeats during a DST
    fall-back while retention deletes oldest-first.
  - **A pre-migration snapshot is taken in `Startup.ApplyMigrations` and a failure aborts startup.**
    `REGARD_MIGRATE=1` is in the Dockerfile, so migrations are applied unattended on every start. Skips
    (not aborts) when there are no pending migrations, on a fresh install with no database, and on SQL
    Server.
  - **`Program.Main` sets `Environment.ExitCode = 1` on a fatal startup failure.** It used to log and
    return, i.e. exit 0 — so `restart: on-failure` never restarted and a refused migration looked like a
    clean shutdown.
- **In-place `VACUUM` is a button and must stay one.** It rewrites the whole file under an exclusive
  lock; other writers block for `busy_timeout` (30 s) and *then* throw, which is the bare-500 symptom
  above. Under WAL the file does not shrink until a checkpoint, so follow it with
  `PRAGMA wal_checkpoint(TRUNCATE)` or the UI reports success while nothing visibly happens. SQLite's
  scratch database goes to `/tmp` — the container's writable layer, not the data volume — so a
  free-space check on `/data` does not cover it.
- **A recurring job has ONE `JobInfo` row that every fire reuses, and it sits `Completed` between
  fires.** So it is indistinguishable by state and age from finished one-shot work, and pruning by age
  deletes it — after which the next fire throws `"Invalid job ID"` at `JobBase.Execute` and that
  recurring job is dead until restart. Harmless at boot (InitJob recreates them); not harmless on a live
  server. **Excluding by `Key` does not work** — `Key` is the bare type name, so a manual sync and the
  nightly one are identical. Exclude the JobIds a live Quartz trigger points at instead
  (`MaintenanceJob.LiveJobIds`, the same scan `RegardScheduler.TryUnschedule` uses).
- **`JobTrackerService.OnJobFailed` ignores `job.Notify`.** Only the retry branch checks it; the
  terminal-failure branch posts unconditionally, and a job scheduled with no `userId` broadcasts to
  every non-admin (`NotificationService.GetRecent`, `Clients.All`). So an unattended job that throws
  notifies the whole install. `MaintenanceJob` works around it by never letting an exception escape
  `ExecuteJob`. Filed in `BACKLOG.md`; fixing it globally changes behaviour for every job type.
- **Recurring jobs are re-scheduled at `now + N` on every boot**, because Quartz's trigger store is
  in-memory. A 24-hour interval on a machine that restarts daily therefore fires *never*. Anything on a
  long interval needs a persisted "last run" and a catch-up (`Server_Maintenance_LastRunUtc`).

## Where durable state lives

Keep decisions/gotchas in files, not the chat buffer:
- `BACKLOG.md` — "later" pile of ideas + known issues.
- `TEST_FINDINGS*.md` — verification notes.
- Plan files under `~/.claude/plans/` for active multi-phase work.

## Working with me on this repo

- Commit to `master`, no feature branch, no AI/`Co-authored-by` attribution. Commit/push only when I ask.
- Prefer small, single-focus plans; I may ask you to "run a review" (adversarial) before I approve one.
- If a request is ambiguous, ask 1–2 pointed questions first. When I hand you a constraint mid-task, fold
  it into this file so I don't repeat it.
</content>
</invoke>
