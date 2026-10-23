# syntax=docker/dockerfile:1
# Fresh Orbit Migrations host: applies EF Core migrations, loads reference data, optionally loads (--sample) or
# purges (--purge-sample) sample data. Runs once and exits; sample data is refused when DOTNET_ENVIRONMENT=Production.
# Build context: repository root.
#   docker build -f infra/docker/migrations.Dockerfile -t fresh-orbit-migrations .
#   docker run --rm -e ConnectionStrings__FreshOrbit=... fresh-orbit-migrations [--sample]
# See api.Dockerfile for the multi-arch and base-image notes.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.102-noble
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.24-extra

FROM --platform=$BUILDPLATFORM ${DOTNET_SDK_IMAGE} AS build
ARG TARGETARCH
WORKDIR /src
# backend/ without tests, bin and obj (.dockerignore); .editorconfig and Directory.*.props shape the build.
COPY backend/ backend/
RUN --mount=type=cache,id=freshorbit-nuget,target=/root/.nuget/packages \
    dotnet publish backend/src/Hosts/FreshOrbit.Migrations/FreshOrbit.Migrations.csproj \
        -c Release --os linux-musl -a "$TARGETARCH" --no-self-contained \
        -p:UseAppHost=false -o /app

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
LABEL org.opencontainers.image.title="fresh-orbit-migrations" \
      org.opencontainers.image.source="https://github.com/MosaSivaMani/fresh-orbit" \
      org.opencontainers.image.licenses="UNLICENSED"
ENV DOTNET_NOLOGO=1 \
    Seeding__ReferenceDataPath=/app/data/reference \
    Seeding__SampleDataPath=/app/data/sample
WORKDIR /app
COPY --from=build /app .
# Reference data (always loaded) and sample data (only with --sample, refused in Production).
COPY backend/data ./data
# Numeric form of the image's non-root `app` user ($APP_UID).
USER 1654:1654
ENTRYPOINT ["dotnet", "FreshOrbit.Migrations.dll"]
