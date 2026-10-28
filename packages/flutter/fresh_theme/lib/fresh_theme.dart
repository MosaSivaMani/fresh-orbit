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

export 'src/blocks.dart';
export 'src/choices.dart';
export 'src/feedback.dart';
export 'src/gallery.dart';
export 'src/header.dart';
export 'src/highlights.dart';
export 'src/layout.dart';
export 'src/look.dart';
export 'src/motion.dart';
export 'src/page.dart';
export 'src/parts.dart';
export 'src/prices.dart';
export 'src/scenery.dart';
export 'src/stickers.dart';
export 'src/titles.dart';

part 'src/tokens.g.dart';
part 'src/fresh_theme_data.dart';
