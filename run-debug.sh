#!/usr/bin/env bash
#
# Run Regard locally (SQLite, no Docker) for development / debugging.
#
#   ./run-debug.sh            run the backend (API) on http://localhost:9585
#   ./run-debug.sh frontend   run the Blazor WASM dev server on http://localhost:5000
#   ./run-debug.sh both       run both (frontend in the background; Ctrl+C stops both)
#
# Data + downloads live in ./.dev (git-ignored). Delete that folder to start fresh.
# Requires: .NET 10 SDK, and python3 + ffmpeg on PATH (for yt-dlp downloads).
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV="$REPO/.dev"

BACKEND_URL="http://localhost:9585"    # the frontend's BACKEND_URL points here
FRONTEND_URL="http://localhost:5000"

# Shared env. Note: ASPNETCORE_URLS is set PER-process below (not exported) so the
# backend and frontend don't fight over the same port in "both" mode.
export ASPNETCORE_ENVIRONMENT=Development
export DataDirectory="$DEV/data"
export DownloadDirectory="$DEV/videos"   # absolute on purpose (storage layer requires it)
export REGARD_MIGRATE=1                   # create/upgrade the SQLite schema on start
mkdir -p "$DataDirectory" "$DownloadDirectory"

backend() {
  echo "==> Backend listening on $BACKEND_URL   (data in $DEV)"
  # --no-launch-profile so our ASPNETCORE_URLS wins over launchSettings.json (which points at :5001/:5000).
  ASPNETCORE_URLS="$BACKEND_URL" dotnet run --no-launch-profile --project "$REPO/Source/Regard.Backend"
}
frontend() {
  echo "==> Frontend listening on $FRONTEND_URL   (talks to API at $BACKEND_URL)"
  ASPNETCORE_URLS="$FRONTEND_URL" dotnet run --no-launch-profile --project "$REPO/Source/Regard.Frontend"
}

case "${1:-backend}" in
  backend)  backend ;;
  frontend) frontend ;;
  both)
    echo "==> Starting frontend (background) + backend (foreground). Ctrl+C stops both."
    frontend & FE=$!
    trap 'kill "$FE" 2>/dev/null || true' EXIT
    backend ;;
  *) echo "usage: $(basename "$0") [backend|frontend|both]"; exit 1 ;;
esac
