#!/bin/sh
# Start of the free public demo container (infra/docker/api-demo.Dockerfile, docs/runbooks/render-demo.md).
#  1. The Migrations host: claims the database only if it is empty or already the demo's (else exit 5), applies
#     migrations and reference data, and loads the Visakhapatnam sample store. Every step skips what exists, so this
#     runs on every start (Render's free plan restarts the container after each sleep).
#  2. The Api, replacing this shell (exec), so it gets the stop signal directly.
# Any failure stops the container; Render shows the log and tries again.
set -eu

cd /app/migrations
dotnet FreshOrbit.Migrations.dll --sample

cd /app/api
exec dotnet FreshOrbit.Api.dll
