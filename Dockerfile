# OpsHub as one container: the API serves the built Angular app from the same origin.
# Build:  docker build --tag opshub .
# Run:    see docs/DEPLOYMENT.md

# ---------- 1. Build the Angular app ----------
FROM node:22-bookworm-slim AS web
WORKDIR /src/web
# Dependencies first, so this layer is reused until package-lock.json changes.
COPY src/OpsHub.Web/package.json src/OpsHub.Web/package-lock.json ./
RUN npm ci
COPY src/OpsHub.Web/ ./
RUN npx ng build

# ---------- 2. Build and publish the API ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
# .editorconfig carries the analyzer settings (e.g. generated migrations are exempt). Without
# it, warnings that are switched off on purpose would fail the build: warnings are errors.
COPY Directory.Build.props .editorconfig ./
COPY src/OpsHub.Domain/OpsHub.Domain.csproj src/OpsHub.Domain/
COPY src/OpsHub.Api/OpsHub.Api.csproj src/OpsHub.Api/
RUN dotnet restore src/OpsHub.Api/OpsHub.Api.csproj
COPY src/OpsHub.Domain/ src/OpsHub.Domain/
COPY src/OpsHub.Api/ src/OpsHub.Api/
RUN dotnet publish src/OpsHub.Api/OpsHub.Api.csproj --configuration Release --no-restore --output /app

# ---------- 3. Runtime image: ASP.NET Core only, no SDK or Node ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /src/web/dist/opshub-web/browser ./wwwroot

# /data holds everything that must survive a restart or an upgrade: the SQLite database and
# the keys that sign session cookies. Mount a volume there.
RUN mkdir /data && chown "$APP_UID" /data
VOLUME /data

# HTTP inside the container; HTTPS is handled by the platform or reverse proxy in front.
# Migrations on startup are safe with SQLite, which only supports a single instance anyway.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__OpsHub="Data Source=/data/opshub.db" \
    DataProtection__KeysDirectory=/data/keys \
    Database__ApplyMigrationsOnStartup=true
EXPOSE 8080

# Run as the image's unprivileged "app" user, not root.
USER $APP_UID
ENTRYPOINT ["dotnet", "OpsHub.Api.dll"]
