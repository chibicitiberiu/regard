#!/usr/bin/env bash
#
# Run Regard locally (SQLite, no Docker) for development / debugging.
#
#   ./run-debug.sh            run the backend (API) on http://localhost:9585
#   ./run-debug.sh frontend   run the Blazor WASM dev server (open the URL it prints)
#   ./run-debug.sh both       run both (frontend in the background; Ctrl+C stops both)
#
# Data + downloads live in ./.dev (git-ignored). Delete that folder to start fresh.
# Requires: .NET 10 SDK, and python3 + ffmpeg on PATH (for yt-dlp downloads).
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV="$REPO/.dev"

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:9585   # matches the frontend's BACKEND_URL
export DataDirectory="$DEV/data"
export DownloadDirectory="$DEV/videos"         # absolute on purpose (storage layer requires it)
export REGARD_MIGRATE=1                         # create/upgrade the SQLite schema on start
mkdir -p "$DataDirectory" "$DownloadDirectory"

backend()  { dotnet run --project "$REPO/Source/Regard.Backend"; }
frontend() { dotnet run --project "$REPO/Source/Regard.Frontend"; }

case "${1:-backend}" in
  backend)  echo "Backend  -> $ASPNETCORE_URLS   (data in $DEV)"; backend ;;
  frontend) echo "Frontend -> open the URL it prints (talks to $ASPNETCORE_URLS)"; frontend ;;
  both)     echo "Frontend (background) + backend (foreground). Ctrl+C stops both."
            frontend & FE=$!
            trap 'kill "$FE" 2>/dev/null || true' EXIT
            backend ;;
  *)        echo "usage: $(basename "$0") [backend|frontend|both]"; exit 1 ;;
esac
