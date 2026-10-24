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
| next | 16.4.2 | MIT | web | Customer website framework (App Router, SSR). **Exception to the two-week rule:** 16.4.0 and earlier carry critical advisories (GHSA-2464-fhr4-w9xw, GHSA-673q-mjm6-r5xr) and high GHSA-p3m5-59mp-jv5r, and `next build` refuses to run on them; 16.4.2 (published 2026-10-15) is the first fixed patch. See decision log |
| react-router | 7.18.4 | MIT | admin | Data router (`createBrowserRouter`, lazy routes). 7.x kept over 8.x: 8 was too new at adoption (2026-10-24) |
| react-hook-form | 7.89.0 | MIT | admin | Forms; server field errors set with `setError` |
| zod | 4.6.5 | MIT | admin | Form schemas (shape checks only; business rules stay on the server) |
| @hookform/resolvers | 5.9.1 | MIT | admin | zod ↔ react-hook-form bridge |
| @fontsource/baloo-2 | 5.3.0 | OFL-1.1 (font), MIT (CSS) | ui (`fonts.css`), playground | Self-hosted Baloo 2 display font, weights 500–800 (D53) |
| @fontsource/inter | 5.3.0 | OFL-1.1 (font), MIT (CSS) | ui (`fonts.css`), playground | Self-hosted Inter body font, weights 400–700 |

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
| @tailwindcss/postcss | 4.3.3 | MIT | Tailwind in Next.js (web) |
| msw | 2.15.0 | MIT | API mocks in web and admin unit tests (Node interceptor only, never shipped; 2.x kept over 3.x, too new at adoption; postinstall skipped via `ignoredBuiltDependencies`) |
| sharp | 0.35.5 | Apache-2.0 | `@fresh/illustrations` `optimize` only (dev): crop, resize, WebP. Its prebuilt libvips binary (`@img/sharp-libvips-*`, LGPL-3.0) is downloaded as a normal npm optional dependency, used only on the developer machine and never shipped; nothing installed on the system |
| @playwright/test | 1.64.0 | Apache-2.0 | End-to-end smoke tests (web and admin; browsers installed with `pnpm exec playwright install chromium`) |

## Planned (not installed yet)

| Tool | Licence | When |
|---|---|---|
| OpenAPI Generator (`dart-dio`, Docker image) | Apache-2.0 | `fresh_api` generation after Phase 3 |
| JetBrains Mono font | OFL-1.1 | Loaded by each app when needed (Inter and Baloo 2 are now installed, see above; Plus Jakarta Sans dropped for Baloo 2 by D53) |
| Baloo 2 and Inter for Flutter | OFL-1.1 | Bundled in `apps/mobile` by U4 |
