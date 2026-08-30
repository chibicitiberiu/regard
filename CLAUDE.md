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

## Frontend / styling

**Edit only `.scss`.** `style.css` (+ `.map`) is generated and gitignored; `AspNetCore.SassCompiler`
recompiles it on build. Source of truth is `wwwroot/css/style.scss` and `wwwroot/css/lib/_*.scss`.

## Testing (how I like it verified)

I ask for changes to be **implemented and tested, usually with Playwright**, not just compiled.

- Playwright with `headless=False` under `DISPLAY=:0`, interpreter `/usr/bin/python3.14`.
- `wait_until="domcontentloaded"` (the app is a SPA; `networkidle` stalls).
- Auth by injecting the token into `localStorage` (`authToken`) via an init script; creds `admin` /
  `Regard!2026`.
- There's an `api.py` helper for driving the backend directly. Real endpoints: login is
  `POST /api/auth/login`; notifications the bell reads are `GET /api/notifications/recent?take=N`
  (not `/list`). When in doubt, read live state through the API rather than the DB file.

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
  - **Chrome will not put a captions button in its control bar.** It files Captions inside the `⋮`
    overflow menu with Download and Picture-in-picture, with no way to promote it. The CC control on the
    watch page is therefore ours, overlaid on the player; it drives the same text tracks, and the
    tracks' `change` event keeps it in step with the browser's own menu.
- **`video.js` is loaded but deliberately not initialised.** Its `autoSetup` re-polls every 1 ms until
  `window load` and wraps any element carrying `data-setup`, so that attribute was removed from
  `Video.razor` — otherwise whether we get a native `<video>` or a video.js player depends on boot
  timing. Everything in `RegardHelpers` drives the raw DOM element.
- **Providers** are a separate project and can't touch options/`HostThrottle` directly — they go through
  `IYoutubeDlService` (`GetAntibotArgs`, `PaceExtractionAsync`).
- **Options**: `OptionDefinition<T>(default, key, configKey, envKey, flags)`; `flags = 0` means
  server-only. Read with `optionManager.GetGlobal` (DB → env → config → default). Nothing hardcoded —
  defaults are option defaults, admin-tunable.

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
