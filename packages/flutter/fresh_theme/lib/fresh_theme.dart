/// Fresh Orbit theme for Flutter.
///
/// Colours, type scale, spacing, radii and durations come from `src/tokens.g.dart`, which is generated
/// from `packages/design-tokens/tokens.json` by `pnpm --filter @fresh/design-tokens build`.
///
/// The rich screens' shared parts (D176, D181) are hand-written: the grid ([FreshLayout], [FreshColumns]), the
/// highlights ([BrushHighlight], [Sparks], [ScriptLine], [SwooshUnderline], [Snowflake]) and the drawn scenery
/// ([FreshScenery], [HarbourScenePainter], [PalmsPainter]) with the owner's real trawler and gull pictures
/// ([FreshTrawler], [FreshGull], [FreshSceneryPictures]; D287). The sign-in look (D281): [FreshAuthPage],
/// [FreshAuthHeader], [FreshAvatarBadge], [FreshPhoneField], [FreshArrowButton] and [FreshCoastScene]; the
/// "Tell us about you" field (D290): [FreshTextField]. The order-placed look (D288): [FreshCelebrationHeader],
/// [FreshTicketCard], [FreshNoteCard] and [FreshOutlineArrowButton]. The sea-floor footer of calm pages (D289):
/// [FreshSeaFloor] and [FreshSeaFloorScroll]. The home's scrolling sections (D293): [FreshSectionHeader],
/// [FreshStepTile] and [FreshHelpCard]. Inline links in a sentence (D299): [FreshLinkText] with [FreshTextPart].
library;

import 'package:flutter/material.dart';

export 'src/auth.dart';
export 'src/blocks.dart';
export 'src/celebration.dart';
export 'src/choices.dart';
export 'src/feedback.dart';
export 'src/fields.dart';
export 'src/gallery.dart';
export 'src/header.dart';
export 'src/highlights.dart';
export 'src/layout.dart';
export 'src/links.dart';
export 'src/look.dart';
export 'src/motion.dart';
export 'src/network_picture.dart';
export 'src/page.dart';
export 'src/parts.dart';
export 'src/prices.dart';
export 'src/scenery.dart';
export 'src/sea_floor.dart';
export 'src/sections.dart';
export 'src/stickers.dart';
export 'src/titles.dart';

part 'src/tokens.g.dart';
part 'src/fresh_theme_data.dart';
