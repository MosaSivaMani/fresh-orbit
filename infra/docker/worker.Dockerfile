# syntax=docker/dockerfile:1
# Fresh Orbit Worker host (Hangfire server, outbox dispatcher, recurring jobs). No HTTP port.
# Build context: repository root.
#   docker build -f infra/docker/worker.Dockerfile -t fresh-orbit-worker .
# See api.Dockerfile for the multi-arch and base-image notes. The Worker references the ASP.NET Core shared
# framework (through FreshOrbit.Hosting), so it runs on the aspnet image too.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.102-noble
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.24-extra

FROM --platform=$BUILDPLATFORM ${DOTNET_SDK_IMAGE} AS build
ARG TARGETARCH
WORKDIR /src
# backend/ without tests, bin and obj (.dockerignore); .editorconfig and Directory.*.props shape the build.
COPY backend/ backend/
RUN --mount=type=cache,id=freshorbit-nuget,target=/root/.nuget/packages \
    dotnet publish backend/src/Hosts/FreshOrbit.Worker/FreshOrbit.Worker.csproj \
        -c Release --os linux-musl -a "$TARGETARCH" --no-self-contained \
        -p:UseAppHost=false -o /app

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
LABEL org.opencontainers.image.title="fresh-orbit-worker" \
      org.opencontainers.image.source="https://github.com/MosaSivaMani/fresh-orbit" \
      org.opencontainers.image.licenses="UNLICENSED"
ENV DOTNET_NOLOGO=1 \
    FileStorage__RootPath=/media
WORKDIR /app
RUN mkdir -p /media && chown "$APP_UID" /media
COPY --from=build /app .
# Numeric form of the image's non-root `app` user ($APP_UID).
USER 1654:1654
ENTRYPOINT ["dotnet", "FreshOrbit.Worker.dll"]
