# Regard — single-container image: net10 ASP.NET backend that also serves the Blazor WASM UI,
# backed by SQLite, with python3 + ffmpeg for yt-dlp downloads. Replaces the old net5 + MSSQL +
# separate-nginx setup.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY Source ./Source
# Publish the API host and the WASM client separately (they're independent projects).
RUN dotnet publish Source/Regard.Backend/Regard.Backend.csproj -c Release -o /app/backend
RUN dotnet publish Source/Regard.Frontend/Regard.Frontend.csproj -c Release -o /app/frontend

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# yt-dlp runs as a python zipapp; ffmpeg is needed for merges + thumbnail conversion;
# ca-certificates for the HTTPS pull of yt-dlp from github. python3-pip is only here to add
# curl_cffi below.
RUN apt-get update && \
    apt-get install -y --no-install-recommends python3 python3-pip ffmpeg ca-certificates curl unzip && \
    rm -rf /var/lib/apt/lists/*

# curl_cffi: browser TLS-fingerprint impersonation for yt-dlp (--impersonate <target>). The yt-dlp
# zipapp runs under the system python3, so install it there. The wheel bundles curl-impersonate, so
# no extra native libs are needed. PEP 668: Debian marks the base env externally-managed, hence
# --break-system-packages (this is a single-purpose container, not a shared system).
RUN pip3 install --break-system-packages --no-cache-dir curl_cffi

# deno: YouTube extraction now requires a JS runtime, otherwise yt-dlp degrades and many
# videos fail to extract. Install the static binary to /usr/local/bin (on PATH for uid 1000).
RUN set -eux; \
    arch="$(dpkg --print-architecture)"; \
    case "$arch" in \
        amd64) deno_arch="x86_64-unknown-linux-gnu" ;; \
        arm64) deno_arch="aarch64-unknown-linux-gnu" ;; \
        *) echo "unsupported arch: $arch" >&2; exit 1 ;; \
    esac; \
    curl -fsSL "https://github.com/denoland/deno/releases/latest/download/deno-${deno_arch}.zip" -o /tmp/deno.zip; \
    unzip /tmp/deno.zip -d /usr/local/bin; \
    chmod +x /usr/local/bin/deno; \
    rm /tmp/deno.zip; \
    /usr/local/bin/deno --version

# Run non-root as uid 1000 (the base image already provides this user); it owns the volumes.
RUN mkdir -p /data /downloads && chown 1000:1000 /data /downloads

WORKDIR /app

# Backend publish output (Regard.Backend.dll, its appsettings.json, wwwroot/img, ...).
COPY --from=build /app/backend ./
# Published Blazor WASM into the served wwwroot (merges over the backend's wwwroot/img).
COPY --from=build /app/frontend/wwwroot/ ./wwwroot/
# Same-origin: the deployed UI talks to its own origin (Program.cs defaults BACKEND_URL to the
# host origin when this is empty).
RUN echo '{"BACKEND_URL":""}' > ./wwwroot/appsettings.json
# NLog config (writes to ${DataDirectory}/Logs, i.e. /data/Logs).
COPY Docker/Backend/nlog-Release.config ./nlog.config

# Plain HTTP behind a TLS-terminating reverse proxy. Absolute data/download dirs (the storage
# layer + Jellyfin path-matching require an absolute DownloadDirectory). REGARD_MIGRATE creates
# the SQLite schema on first run.
ENV ASPNETCORE_URLS=http://+:8080 \
    DataDirectory=/data \
    DownloadDirectory=/downloads \
    REGARD_MIGRATE=1

EXPOSE 8080
VOLUME ["/data", "/downloads"]

USER 1000
ENTRYPOINT ["dotnet", "Regard.Backend.dll"]
