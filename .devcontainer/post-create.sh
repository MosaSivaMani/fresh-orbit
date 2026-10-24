#!/usr/bin/env bash
# Runs once when the Codespace (dev container) is created: pnpm through corepack, front-end and .NET restore.
# Starting the stack is a separate step: bash infra/scripts/codespace-up.sh
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# corepack ships with Node 22; it provides the pnpm version pinned in package.json ("packageManager").
corepack enable 2> /dev/null || sudo env "PATH=$PATH" corepack enable
corepack install

pnpm install --frozen-lockfile
dotnet restore backend/FreshOrbit.slnx

echo
echo "Ready. Start the store with:  bash infra/scripts/codespace-up.sh"
