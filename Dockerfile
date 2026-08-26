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
# ca-certificates for the HTTPS pull of yt-dlp from github.
RUN apt-get update && \
    apt-get install -y --no-install-recommends python3 ffmpeg ca-certificates && \
    rm -rf /var/lib/apt/lists/*

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
