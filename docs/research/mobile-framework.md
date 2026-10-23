# Mobile framework (decided 2026-10-23)

Decision: D28. Both options build Android and iOS from one codebase.

| Topic | Expo (React Native) | Flutter (chosen) |
|---|---|---|
| Language | TypeScript (shared with web) | Dart |
| Android + iOS | Yes | Yes |
| Performance / UI consistency | Good | Very good, same rendering on both |
| Sharing with web | Can import TS packages | Generated Dart client + generated theme from the same OpenAPI and tokens |
| Business rules | Backend only | Backend only |
| Push | Expo Push | Firebase Cloud Messaging (free service, not open-source) |
| Team familiarity | — | Owner already works with Flutter |

## How sharing works with Flutter
| Shared thing | Source | Flutter output |
|---|---|---|
| API types and calls | Backend OpenAPI | `fresh_api` via openapi-generator `dart-dio` (Apache-2.0) |
| Colours, type, spacing | `packages/design-tokens` JSON | Generated `ThemeData` Dart file |
| Business rules | Backend | Nothing to share; app renders server values |
