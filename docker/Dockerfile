# syntax=docker/dockerfile:1
#
# DAFTECH CRM — Backend API (ASP.NET Core 10)
# Multi-stage build: restore/build/publish in the SDK image, run on the
# much smaller ASP.NET runtime image.
#
# Build context must be the repo's `api/DaftechCrm` directory, e.g.:
#   docker build -f docker/Dockerfile -t daftech-crm-api:latest .
# (docker-compose.yml in this folder sets the context correctly already.)

# ---------------------------------------------------------------------------
# Stage 1: build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Pin the exact SDK feature band used elsewhere (see ../global.json) so local
# and CI builds can't silently drift onto a newer/older .NET 10 SDK.
COPY global.json ./

# Copy csproj files first so restore is cached unless dependencies change.
COPY DaftechCrm.sln ./
COPY src/DaftechCrm.Domain/DaftechCrm.Domain.csproj src/DaftechCrm.Domain/
COPY src/DaftechCrm.Application/DaftechCrm.Application.csproj src/DaftechCrm.Application/
COPY src/DaftechCrm.Infrastructure/DaftechCrm.Infrastructure.csproj src/DaftechCrm.Infrastructure/
COPY src/DaftechCrm.Api/DaftechCrm.Api.csproj src/DaftechCrm.Api/

RUN dotnet restore src/DaftechCrm.Api/DaftechCrm.Api.csproj

# Now copy the rest of the source and publish.
COPY src/ src/

RUN dotnet publish src/DaftechCrm.Api/DaftechCrm.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Stage 2: runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as a non-root user.
RUN addgroup --system --gid 1000 daftech \
    && adduser --system --uid 1000 --ingroup daftech --shell /bin/false daftech

# Uploaded files live here; keep it a separate, writable volume mount point.
RUN mkdir -p /var/daftech/uploads /app/logs \
    && chown -R daftech:daftech /var/daftech/uploads /app/logs /app

COPY --from=build --chown=daftech:daftech /app/publish .

USER daftech

# Local/compose default. Render (and most PaaS hosts) inject their own PORT
# env var at container start and route traffic to it — the shell form
# ENTRYPOINT below re-reads $PORT at runtime so both cases work without
# rebuilding the image.
ENV PORT=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT}/health/live || exit 1

ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} exec dotnet DaftechCrm.Api.dll"]