# Regard

Regard is a self hosted personal video management platform, which allows you to keep track of, download automatically and watch the content you want to see.

![image](https://user-images.githubusercontent.com/5184913/116914401-5c8dec00-ac53-11eb-8233-a5fa6ba0f061.png)

The project is still in development, many things are buggy and don't work properly.

If you need any help, or would like to discuss with us, you are welcome to [join our Discord](https://discord.gg/mZgjdXEfY6).

## Installation (Docker)

Regard runs as a **single container**: the .NET 10 backend serves both the REST API and the Blazor web UI, stores everything in **SQLite**, and manages `yt-dlp` itself. There is no separate database or frontend container. You only need Docker with the Compose plugin.

### Quick start

1. Get the repository (or just copy `docker-compose.yml` and `Dockerfile`).

2. Create the downloads directory on the host and give it to UID `1000`. The container runs as a non-root user (uid 1000), and a bind mount keeps the **host** directory's ownership — so this step is required or downloads will fail:

   ```sh
   sudo mkdir -p /srv/regard/downloads
   sudo chown 1000:1000 /srv/regard/downloads
   ```

3. Build and start:

   ```sh
   docker compose up -d --build
   ```

4. Open `http://<host>:8999` and register the first account.

On first boot the container downloads `yt-dlp` from GitHub (and updates it daily), so it needs outbound HTTPS access.

### What the compose file provides

- One `regard` service on host port **8999** → container **8080**. It serves plain HTTP — put a reverse proxy in front of it for TLS (see below).
- A named volume **`regard-data` → `/data`**: the SQLite database, thumbnails, the managed `yt-dlp` binary, and logs. Persistent — do not delete it.
- A bind mount **`/srv/regard/downloads` → `/downloads`**: the downloaded videos. This path is absolute on purpose (see *Using it with Jellyfin*).

### Configuration (environment variables)

Set these under the service's `environment:` in `docker-compose.yml`:

| Variable | Default | Purpose |
|---|---|---|
| `DataDirectory` | `/data` | App data (DB, thumbnails, yt-dlp, logs). Keep it on the volume. |
| `DownloadDirectory` | `/downloads` | Video storage. **Must be an absolute path.** |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address inside the container. |
| `REGARD_MIGRATE` | `1` | Apply database migrations on start (needed on first run). A snapshot of the database is taken first — see *Backups*. |
| `REGARD_BACKUP_PREMIGRATION` | `true` | Snapshot the database before applying migrations, and **refuse to migrate** if that snapshot fails. Set `false` only if you back up by other means. SQLite only. |
| `REGARD_BACKUP_ENABLED` | `true` | Take a nightly database snapshot. |
| `REGARD_BACKUP_KEEP_COUNT` | `7` | How many routine snapshots to keep. Pre-migration snapshots are kept separately. |
| `REGARD_MAINTENANCE_ENABLED` | `true` | Run the nightly housekeeping sweep (snapshot, prune job history and notifications, clean up yt-dlp logs). |
| `REGARD_MAINTENANCE_INTERVAL_HOURS` | `24` | How often that sweep runs. Changing it needs a restart. |
| `REGARD_ALLOW_REGISTRATIONS` | `true` | Allow new account sign-ups. Set `false` after creating your account to lock it down (the first account is always allowed and becomes the admin). |
| `JWT__Secret` | *(generated)* | JWT signing secret. Leave unset to auto-generate + persist one at `{DataDirectory}/jwt-secret`. |
| `Metadata__Enabled` | `false` | Write Jellyfin/Kodi NFO sidecars + poster/thumbnail images and name files `SxxExx - Title`. |
| `Jellyfin__Enabled` | `false` | Enable watched-sync (poll Jellyfin; mark played videos watched → delete + refill). |
| `Jellyfin__BaseUrl` | | e.g. `http://jellyfin:8096`. |
| `Jellyfin__ApiKey` | | Jellyfin admin API key (Dashboard → API Keys). |
| `Jellyfin__JellyfinUser` | | The Jellyfin account whose *played* state to read. |
| `Jellyfin__RegardUser` | | The Regard account that owns the videos (defaults to `JellyfinUser`). |
| `Jellyfin__PollSchedule` | `0 0/10 * * * ?` | Quartz cron expression for the poll interval. |

### First run & security

- The **first account you register becomes the administrator.** Register it immediately after starting the container, then set `REGARD_ALLOW_REGISTRATIONS=false` to close public sign-ups.
- On first boot Regard generates a random JWT signing secret and stores it at `{DataDirectory}/jwt-secret` (override with `JWT__Secret`). Keep the `regard-data` volume — losing it forces everyone to log in again.
- The container serves plain **HTTP**; always put it behind a TLS-terminating reverse proxy when exposing it to the internet.

### Backups

Regard snapshots its own database into **`{DataDirectory}/Backups`** — nightly, and always immediately
before applying a schema migration. Snapshots are named `Regard-<UTC timestamp>.db`, and the
pre-migration ones `Regard-premigration-<UTC timestamp>.db`.

They are taken with SQLite's `VACUUM INTO`, so they are consistent point-in-time copies made without
stopping the app, and each one is a single self-contained file — there is no `-wal`/`-shm` to keep
alongside it. Each is verified with `PRAGMA integrity_check` before it replaces the previous one.

Two things to be aware of:

- **The default location is inside the same volume as the database it protects.** That survives
  recreating the container, and nothing else — `docker volume rm regard-data` takes the database and
  every snapshot with it. For real safety, bind-mount somewhere off-box: uncomment the
  `- /srv/regard/backups:/data/Backups` line in `docker-compose.yml` (the host directory must be
  writable by uid 1000, same as the downloads mount).
- **On SQL Server none of this happens.** There is no `VACUUM INTO`, and `BACKUP DATABASE` writes to
  the database server's own filesystem rather than Regard's, so snapshots — including the
  pre-migration one — are skipped with a log line. Back up by your usual SQL Server means.

Only files matching Regard's own naming are ever deleted by retention, so your own copies can safely
sit in the same directory.

#### Restoring

Restoring is deliberately manual — a wrong click here is not recoverable.

1. Stop the container: `docker compose stop regard`
2. Replace `/data/Regard.db` with the snapshot you want, renamed to `Regard.db`.
3. **Delete `/data/Regard.db-wal` and `/data/Regard.db-shm` if they exist.** This step is not
   optional: leaving a stale write-ahead log next to a restored database makes SQLite replay the old
   log over the new file, which will corrupt it.
4. Start it again: `docker compose start regard`
5. Log in and check your subscriptions and video counts look right.

If the app refuses to start with *"Refusing to apply N migration(s)"*, the pre-migration snapshot
could not be written — usually a full disk or a permissions problem on the backup directory. Fix that
and start again, or set `REGARD_BACKUP_PREMIGRATION=false` to migrate without one.

### Using it with Jellyfin

Regard's Jellyfin integration matches videos **by full file path**, so Jellyfin must see the same files at the same path. Mount the same host downloads directory into Jellyfin at `/downloads` and point a **Shows** library at it (with the NFO metadata reader enabled). `docker-compose.yml` includes a commented Jellyfin service showing the shared mount.

- With `Metadata__Enabled=true`, each channel appears as a Show and each video as an Episode, with title, artwork, air date, and episode number.
- With `Jellyfin__Enabled=true`, marking a video *played* in Jellyfin makes Regard mark it watched — which (per the subscription's settings) deletes the file and pulls the next video into the download window.

### Reverse proxy / TLS

The container serves plain HTTP on port 8080. Terminate TLS at your reverse proxy (nginx / Caddy / Traefik) and forward to the container — no in-container HTTPS configuration is required.

### Upgrading

```sh
docker compose up -d --build   # rebuild the image and restart
```

Your data lives in the `regard-data` volume and the downloads bind mount, so upgrades preserve it; `REGARD_MIGRATE=1` applies any new database migrations on start.

## Development setup

Required software:

* **.NET 10 SDK** (the version is pinned in `global.json`)
* **Python 3** and **ffmpeg** on `PATH` (used to run `yt-dlp` and merge downloads)
* An editor of your choice — **Visual Studio 2022**, **VS Code**, or **Rider**
* (optional) **Entity Framework CLI** for working with migrations: `dotnet tool install --global dotnet-ef`

The backend uses **SQLite by default**, so no database server is required. (To use SQL Server instead, set the `ConnectionStrings:SqlServer` connection string.)

Steps:

1. Clone the repository.
2. (optional) Adjust `DataDirectory` / `DownloadDirectory` in `Source/Regard.Backend/appsettings.json`. The SQLite database file is created automatically under `DataDirectory`; migrations are applied on start when the `REGARD_MIGRATE` environment variable is set.
3. Run the backend (from the repo root):

   ```sh
   REGARD_MIGRATE=1 dotnet run --project Source/Regard.Backend
   ```

4. Run the frontend in a second terminal:

   ```sh
   dotnet run --project Source/Regard.Frontend
   ```

   The frontend reads its backend URL from `Source/Regard.Frontend/wwwroot/appsettings.json` (`BACKEND_URL`), which points at the local backend by default.

Alternatively, open `Source/Regard.sln` in Visual Studio and set both `Regard.Backend` and `Regard.Frontend` as startup projects (right-click the solution → *Set startup projects…* → *Multiple startup projects* → set both to *Start*).

