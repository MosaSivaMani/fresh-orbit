# Front-end dependencies

Exact versions (`save-exact`), each published at least two weeks before adoption (2026-10-23). Licences checked
from the npm registry. Runtime = shipped to browsers/apps; dev = build, lint and test only.

## Runtime

| Package | Version | Licence | Used by | Purpose |
|---|---|---|---|---|
| react, react-dom | 19.3.0 | MIT | ui (peer), playground | UI library |
| radix-ui | 1.7.0 | MIT | ui | Accessible primitives (Dialog, Select, Popover, Checkbox, Radio, Switch, Tabs, Tooltip, Toast, Label, Slot) |
| cmdk | 1.1.1 | MIT | ui | Combobox filtering and keyboard list |
| @tanstack/react-table | 8.21.3 | MIT | ui | DataTable (headless) |
| @tanstack/react-query | 5.104.1 | MIT | core (peer), apps | Server state; `createQueryClient` |
| lucide-react | 1.54.0 | ISC | ui, playground | Icons |
| class-variance-authority | 0.7.1 | Apache-2.0 | ui | Variant class maps |
| clsx | 2.1.1 | MIT | ui | Class joining |
| tailwind-merge | 3.7.0 | MIT | ui | Conflict-free class merging |
| openapi-fetch | 0.17.0 | MIT | api-client | Typed fetch client for generated paths |
| tailwindcss | 4.3.3 | MIT | apps (CSS build) | Styling engine (output CSS ships) |

## Development

| Package | Version | Licence | Purpose |
|---|---|---|---|
| pnpm | 10.34.6 | MIT | Package manager (`packageManager` field) |
| turbo | 2.11.7 | MIT | Task runner |
| typescript | 5.9.3 | Apache-2.0 | Types (see design README §2 for why not 7.x) |
| tsup | 8.5.1 | MIT | Library bundling (core, ui, api-client) |
| vite | 8.3.4 | MIT | Playground dev server/build |
| @vitejs/plugin-react | 6.1.2 | MIT | React in Vite |
| @tailwindcss/vite | 4.3.3 | MIT | Tailwind in Vite |
| vitest | 5.0.3 | MIT | Tests |
| jsdom | 30.1.2 | MIT | DOM for component tests |
| @testing-library/react | 16.3.3 | MIT | Component tests |
| @testing-library/dom | 10.4.2 | MIT | Queries |
| @testing-library/user-event | 14.6.7 | MIT | User interaction |
| @testing-library/jest-dom | 7.0.1 | MIT | DOM matchers |
| axe-core | 4.14.0 | MPL-2.0 | Accessibility checks in tests only (file-level copyleft; unmodified, not shipped) |
| openapi-typescript | 7.13.0 | MIT | Generate `schema.d.ts` from OpenAPI |
| eslint | 9.39.5 | MIT | Linting (ESLint 9: jsx-a11y not yet on 10) |
| @eslint/js | 9.39.5 | MIT | Recommended JS rules |
| typescript-eslint | 8.71.1 | MIT | TS rules/parser |
| eslint-plugin-react-hooks | 7.1.1 | MIT | Hooks rules |
| eslint-plugin-jsx-a11y | 6.10.2 | MIT | Accessibility lint |
| eslint-plugin-react-refresh | 0.5.7 | MIT | HMR-safe exports (apps) |
| @next/eslint-plugin-next | 16.4.0 | MIT | Next.js rules (`eslint/next` preset) |
| eslint-config-prettier | 10.1.8 | MIT | Disable formatting rules |
| globals | 17.13.0 | MIT | Global variable sets |
| prettier | 3.9.9 | MIT | Formatting |
| prettier-plugin-tailwindcss | 0.8.1 | MIT | Class sorting |
| @types/react, @types/react-dom | 19.3.0 | MIT | Types |
| @types/node | 26.6.5 | MIT | Types |

## Planned (not installed yet)

| Tool | Licence | When |
|---|---|---|
| OpenAPI Generator (`dart-dio`, Docker image) | Apache-2.0 | `fresh_api` generation after Phase 3 |
| Inter, Plus Jakarta Sans, JetBrains Mono fonts | OFL-1.1 | Loaded by each app (web: self-hosted; mobile: bundled) |
