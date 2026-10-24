# syntax=docker/dockerfile:1
# Fresh Orbit FREE PUBLIC DEMO (Render free web service + Neon free Postgres; docs/runbooks/render-demo.md).
# Sample data only, never real customers. Not used by docker-compose.prod.yml or deploy.yml.
# Build context: repository root.
#   docker build -f infra/docker/api-demo.Dockerfile -t fresh-orbit-api-demo .
#
# One container, one process at a time (512 MB): api-demo-start.sh runs the Migrations host (migrations, reference
# data, --sample; idempotent, and it claims only an empty database: FreshOrbit.Hosting/DemoDatabase.cs) and then replaces itself with the
# Api. The Api runs the Worker's background jobs in-process (appsettings.Demo.json, Hosting:RunBackgroundJobsInApi).
# ASPNETCORE_ENVIRONMENT and DOTNET_ENVIRONMENT are fixed to Demo here, so this image can never run as Production.
#
# x64 only (Render builds and runs linux/amd64). No BuildKit cache mounts, so any Docker builder works.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.102-noble
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.24-extra

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src
# backend/ without tests, bin and obj (.dockerignore); .editorconfig and Directory.*.props shape the build.
COPY backend/ backend/
# Two folders: each host keeps its own appsettings.json and deps.json. ReadyToRun (precompiled) shortens the start
# after every sleep on a small free CPU.
RUN dotnet publish backend/src/Hosts/FreshOrbit.Api/FreshOrbit.Api.csproj \
        -c Release -r linux-musl-x64 --no-self-contained -p:UseAppHost=false -p:PublishReadyToRun=true -o /app/api \
    && dotnet publish backend/src/Hosts/FreshOrbit.Migrations/FreshOrbit.Migrations.csproj \
        -c Release -r linux-musl-x64 --no-self-contained -p:UseAppHost=false -p:PublishReadyToRun=true -o /app/migrations

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
LABEL org.opencontainers.image.title="fresh-orbit-api-demo" \
      org.opencontainers.image.source="https://github.com/MosaSivaMani/fresh-orbit" \
      org.opencontainers.image.licenses="UNLICENSED"
# Workstation GC and memory-conserving mode keep both hosts well inside the free plan's 512 MB.
ENV ASPNETCORE_ENVIRONMENT=Demo \
    DOTNET_ENVIRONMENT=Demo \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_NOLOGO=1 \
    DOTNET_gcServer=0 \
    DOTNET_GCConserveMemory=7 \
    TZ=UTC \
    FileStorage__RootPath=/media \
    Seeding__ReferenceDataPath=/app/data/reference \
    Seeding__SampleDataPath=/app/data/sample
WORKDIR /app
# /media is the container's own disk: emptied by every restart. Missing sample images are drawn again by the Api
# (UseFreshOrbitDemoMedia); the Migrations host re-renders the banners.
RUN mkdir -p /media && chown "$APP_UID" /media
COPY --from=build /app/api ./api
COPY --from=build /app/migrations ./migrations
COPY backend/data ./data
COPY infra/docker/api-demo-start.sh ./start.sh
# Numeric form of the image's non-root `app` user ($APP_UID).
USER 1654:1654
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=180s --retries=5 \
    CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:8080/health"]
ENTRYPOINT ["/bin/sh", "/app/start.sh"]
