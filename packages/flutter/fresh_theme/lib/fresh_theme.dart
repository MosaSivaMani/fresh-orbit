/// Fresh Orbit theme for Flutter.
///
/// Colours, type scale, spacing, radii and durations come from `src/tokens.g.dart`, which is generated
/// from `packages/design-tokens/tokens.json` by `pnpm --filter @fresh/design-tokens build`.
///
/// The rich screens' shared parts (D176, D181) are hand-written: the grid ([FreshLayout], [FreshColumns]), the
/// highlights ([BrushHighlight], [Sparks], [ScriptLine], [SwooshUnderline], [Snowflake]) and the drawn scenery
/// ([FreshScenery], [HarbourScenePainter], [PalmsPainter]).
library;

import 'package:flutter/material.dart';

export 'src/parts.dart';
export 'src/highlights.dart';
export 'src/layout.dart';
export 'src/scenery.dart';

part 'src/tokens.g.dart';
part 'src/fresh_theme_data.dart';
