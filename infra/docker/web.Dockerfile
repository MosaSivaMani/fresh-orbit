# syntax=docker/dockerfile:1
# Fresh Orbit customer website (Next.js, standalone output) from the pnpm + Turborepo monorepo.
# Build context: repository root.
#   docker build -f infra/docker/web.Dockerfile \
#     --build-arg NEXT_PUBLIC_SITE_URL=https://example.in -t fresh-orbit-web .
#
# How apps/web fits (docs/design/web.md):
#   - next.config switches to `output: "standalone"` (traced from the repository root) when NEXT_OUTPUT=standalone,
#     so the server lands at .next/standalone/apps/web/server.js.
#   - NEXT_PUBLIC_SITE_URL (canonical URLs, sitemap, robots) is inlined at build time: build argument.
#   - FRESH_API_ORIGIN (server-side only; the browser uses this site's /api/v1 proxy and /bff/auth handlers) is read
#     at runtime: set by docker-compose.prod.yml to http://api:8080.
#
# The build stage runs on the TARGET platform (QEMU in CI for arm64) because Next's standalone output can carry
# platform-specific native modules (e.g. sharp). Pruning runs on the build platform.

ARG NODE_IMAGE=node:22.23.3-alpine3.24

FROM --platform=$BUILDPLATFORM ${NODE_IMAGE} AS prune
ARG TURBO_VERSION=2.11.7
ARG WEB_PACKAGE=@fresh/web
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /repo
RUN corepack enable
COPY . .
RUN pnpm dlx "turbo@${TURBO_VERSION}" prune "${WEB_PACKAGE}" --docker

FROM ${NODE_IMAGE} AS build
ARG WEB_PACKAGE=@fresh/web
ARG WEB_DIR=apps/web
ARG NEXT_PUBLIC_SITE_URL=
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0 \
    NEXT_TELEMETRY_DISABLED=1 \
    NEXT_OUTPUT=standalone \
    NEXT_PUBLIC_SITE_URL=${NEXT_PUBLIC_SITE_URL}
WORKDIR /repo
RUN corepack enable
# Dependency layer: only package manifests and the pruned lockfile, so source edits keep the install cached.
COPY --from=prune /repo/out/json/ ./
COPY --from=prune /repo/out/pnpm-lock.yaml ./pnpm-lock.yaml
RUN --mount=type=cache,id=freshorbit-pnpm,target=/pnpm/store \
    pnpm install --frozen-lockfile --store-dir /pnpm/store
COPY --from=prune /repo/out/full/ ./
# Workspace dependencies through turbo, then the app itself directly: turbo's strict env mode would hide
# NEXT_OUTPUT from `next build`.
RUN pnpm turbo run build --filter="${WEB_PACKAGE}^..." \
    && pnpm --filter "${WEB_PACKAGE}" run build \
    && mkdir -p "${WEB_DIR}/public" \
    && mkdir -p /out \
    && cp -R "${WEB_DIR}/.next/standalone/." /out/ \
    && mkdir -p "/out/${WEB_DIR}/.next" \
    && cp -R "${WEB_DIR}/.next/static" "/out/${WEB_DIR}/.next/static" \
    && cp -R "${WEB_DIR}/public" "/out/${WEB_DIR}/public" \
    && printf 'require("./%s/server.js");\n' "${WEB_DIR}" > /out/server.js

FROM ${NODE_IMAGE} AS runtime
LABEL org.opencontainers.image.title="fresh-orbit-web" \
      org.opencontainers.image.source="https://github.com/MosaSivaMani/fresh-orbit" \
      org.opencontainers.image.licenses="UNLICENSED"
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0
WORKDIR /app
COPY --from=build --chown=1000:1000 /out/ ./
USER 1000:1000
EXPOSE 3000
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
    CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:3000/"]
# /app/server.js is a one-line launcher for <WEB_DIR>/server.js (Next's standalone server chdirs to its own folder).
CMD ["node", "server.js"]
