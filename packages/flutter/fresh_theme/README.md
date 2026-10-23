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

Requires Flutter 3.32+ (uses `CardThemeData`). Not yet compiled in CI: Flutter is not installed on the
build machine at Phase 5; run `flutter analyze` here when `apps/mobile` is created (Phase 8).
