# syntax=docker/dockerfile:1
# Fresh Orbit admin dashboard: Vite static build served by a tiny non-root Caddy (port 8080, no TLS, no admin
# API). The edge Caddy (infra/caddy/Caddyfile) proxies admin.<domain> to it and adds the security headers.
# Build context: repository root.
#   docker build -f infra/docker/admin.Dockerfile \
#     --build-arg VITE_API_BASE_URL=https://api.example.in -t fresh-orbit-admin .
#
# Requirements on apps/admin (owned by the admin team): `vite build` writes to apps/admin/dist; hashed assets
# under dist/assets/; values the browser needs come from VITE_* build arguments (inlined at build time).
# The output is platform-independent, so the build runs on the build platform (no QEMU).

ARG NODE_IMAGE=node:22.23.3-alpine3.24
ARG CADDY_IMAGE=caddy:2.11.6-alpine

FROM --platform=$BUILDPLATFORM ${NODE_IMAGE} AS prune
ARG TURBO_VERSION=2.11.7
ARG ADMIN_PACKAGE=@fresh/admin
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /repo
RUN corepack enable
COPY . .
RUN pnpm dlx "turbo@${TURBO_VERSION}" prune "${ADMIN_PACKAGE}" --docker

FROM --platform=$BUILDPLATFORM ${NODE_IMAGE} AS build
ARG ADMIN_PACKAGE=@fresh/admin
ARG ADMIN_DIR=apps/admin
ARG VITE_API_BASE_URL=
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0 \
    VITE_API_BASE_URL=${VITE_API_BASE_URL}
WORKDIR /repo
RUN corepack enable
COPY --from=prune /repo/out/json/ ./
COPY --from=prune /repo/out/pnpm-lock.yaml ./pnpm-lock.yaml
RUN --mount=type=cache,id=freshorbit-pnpm,target=/pnpm/store \
    pnpm install --frozen-lockfile --store-dir /pnpm/store
COPY --from=prune /repo/out/full/ ./
RUN pnpm turbo run build --filter="${ADMIN_PACKAGE}" \
    && cp -R "${ADMIN_DIR}/dist" /out

FROM ${CADDY_IMAGE} AS runtime
LABEL org.opencontainers.image.title="fresh-orbit-admin" \
      org.opencontainers.image.source="https://github.com/MosaSivaMani/fresh-orbit" \
      org.opencontainers.image.licenses="UNLICENSED"
# Caddy writes nothing for a plain HTTP file server, but point its data/config dirs somewhere writable anyway.
ENV XDG_DATA_HOME=/tmp/caddy-data \
    XDG_CONFIG_HOME=/tmp/caddy-config
COPY infra/docker/admin.Caddyfile /etc/caddy/Caddyfile
COPY --from=build /out /srv
USER 65534:65534
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=5 \
    CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:8080/"]
CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
