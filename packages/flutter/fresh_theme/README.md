# fresh_theme (Dart)

Flutter `ThemeData` for Fresh Orbit, built from the shared design tokens.

- `lib/src/tokens.g.dart` is **generated** from `packages/design-tokens/tokens.json` by
  `pnpm --filter @fresh/design-tokens build` (also part of `pnpm build`). Do not edit it.
- `lib/src/fresh_theme_data.dart` maps tokens to Material 3 (`ColorScheme`, `TextTheme`, buttons, inputs, cards).

```dart
import 'package:fresh_theme/fresh_theme.dart';

MaterialApp(theme: FreshTheme.light(), darkTheme: FreshTheme.dark(), themeMode: ThemeMode.system);

final c = Theme.of(context).extension<FreshColors>()!; // success, warning, highlight, accentSubtle, ...
const gap = FreshSpacing.s4;                            // 16
```

Fonts: the theme names Inter / Plus Jakarta Sans with platform fallbacks. The app bundles the font files
(OFL licence) under `apps/mobile` and declares them in its `pubspec.yaml`.

## Shared layout and highlights (D181)

Every screen follows the same theme, so the layout numbers and the hand-drawn highlights live here, not in a
screen. They are separate libraries exported from `fresh_theme.dart` (not `part`s, so the generator never
touches them). All decorations are static (no animation) and excluded from semantics.

| File | Parts |
|---|---|
| `lib/src/layout.dart` | `FreshLayout`: design width 390, gutter 16, card gap 12, section gap 24, radii (card 20, hero 24, tile 16, bar 18, menu 22, pill 99), 46 dp icon buttons, `page` padding, `contentWidth(w)`, `columnWidth(w, n, gap:)`. `FreshColumns`: N equal columns with a gap; `stretch: true` makes them as tall as the tallest |
| `lib/src/highlights.dart` | `FreshHighlightColors` (brush yellow, coral, deep coral, ink, paper). `BrushHighlight` (the yellow brush behind a word, tilt in degrees), `Sparks` (three short dashes, 34 × 30 × scale), `Snowflake`, `SwooshUnderline`, `ScriptLine` (a handwritten line, tilted, with an optional swoosh; the app passes its Dancing Script family) |
| `lib/src/scenery.dart` | `FreshScenery` canvas helpers (bird, cloud, hills, trawler, sea bands, palm) shared by the onboarding drawings and the home; `HarbourScene` (222 × 150 design box: sky, sun, hills, sea, three boats, a palm, birds); `PalmsPainter` + `ScenePalm` for soft palms along a screen edge |

Font families are passed in by the app (the package bundles no fonts). Tests for these parts sit in
`apps/mobile/test/widgets/fresh_theme_parts_test.dart`, because CI's mobile job runs only `apps/mobile`.

Requires Flutter 3.32+ (uses `CardThemeData`). Compiled and analyzed in CI through `apps/mobile` (the
"Mobile app analyze and test" job).
