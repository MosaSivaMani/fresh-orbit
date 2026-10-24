# syntax=docker/dockerfile:1
# Fresh Orbit Api host (REST API, serves /media/* when Caddy does not).
# Build context: repository root.
#   docker build -f infra/docker/api.Dockerfile -t fresh-orbit-api .
# Multi-arch: the SDK stage runs on the build machine's platform and cross-publishes for TARGETARCH
# (no QEMU needed for the .NET build); the runtime stage is the target platform's image.
#
# Runtime image: Alpine with tzdata (-extra) because Settings, Reporting and Payments resolve IANA time zones
# (Asia/Kolkata). SkiaSharp's linux-musl native assets ship in the NuGet package. Runs as the non-root `app`
# user (UID 1654) that the .NET images define.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.102-noble
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.24-extra

FROM --platform=$BUILDPLATFORM ${DOTNET_SDK_IMAGE} AS build
ARG TARGETARCH
WORKDIR /src
# backend/ without tests, bin and obj (.dockerignore); .editorconfig and Directory.*.props shape the build.
COPY backend/ backend/
RUN --mount=type=cache,id=freshorbit-nuget,target=/root/.nuget/packages \
    dotnet publish backend/src/Hosts/FreshOrbit.Api/FreshOrbit.Api.csproj \
        -c Release --os linux-musl -a "$TARGETARCH" --no-self-contained \
        -p:UseAppHost=false -o /app

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
LABEL org.opencontainers.image.title="fresh-orbit-api" \
      org.opencontainers.image.source="https://github.com/MosaSivaMani/fresh-orbit" \
      org.opencontainers.image.licenses="UNLICENSED"
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_NOLOGO=1 \
    FileStorage__RootPath=/media
WORKDIR /app
# /media is the file-storage volume (D21) and /keys the Data Protection key ring (E2E-3); create both owned by the app
# user so a new named volume inherits the owner (and /keys its 700 mode: only the app user can read the keys).
RUN mkdir -p /media /keys && chown "$APP_UID" /media /keys && chmod 700 /keys
COPY --from=build /app .
# Numeric form of the image's non-root `app` user ($APP_UID).
USER 1654:1654
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
    CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:8080/health"]
ENTRYPOINT ["dotnet", "FreshOrbit.Api.dll"]
