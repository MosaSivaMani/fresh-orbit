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
| `lib/src/layout.dart` | `FreshLayout`: design width 390, gutter 16, card gap 12, section gap 24, radii (card 20, hero 24, tile 16, bar 18, menu 22, pill 99), 46 dp icon buttons, `page` padding, `contentWidth(w)`, `columnWidth(w, n, gap:)`. `FreshColumns`: N equal columns with a gap; `stretch: true` makes them as tall as the tallest (laid out twice, no intrinsic sizes; the tallest height is a minimum, not a tight height, so a column whose content grows later measures all of them again instead of overflowing). `FreshFitWidth`: shows a page at the full width and, when it is taller than the space, lays it out wider and scales it down, so it never leaves side gaps like a `FittedBox` (D182) |
| `lib/src/highlights.dart` | `FreshHighlightColors` (brush yellow, coral, deep coral, ink, paper). `BrushHighlight` (the yellow brush behind a word, tilt in degrees), `Sparks` (three short dashes, 34 × 30 × scale), `Snowflake`, `SwooshUnderline`, `ScriptLine` (a handwritten line, tilted, with an optional swoosh; the app passes its Dancing Script family) |
| `lib/src/parts.dart` | Reusable parts (D207); screens use them and never copy them. `FreshOneLine`: one line fitted to its width by a smaller font size (never a FittedBox, D195), as tall as the full-size line, wrapping (never cut) below `minFit`. `FreshChip` (yellow chip with icon), `FreshPill` (paper pill), `FreshFeatureCard` (the home's small card; its picture sits under the title, never over it), `FreshCategoryTile` (50 dp circle, name in one size on up to two lines), `FreshSpecialCard` (offer card: picture, one-line title, two-line description, badge, chevron), `FreshCartBar` (floating cart bar; only line pictures that exist), `FreshGrowTail` (a head and a tail that grows into the room left, so a page fills the screen, D209) |
| `lib/src/sections.dart` | The home's scrolling sections below the first screen (D293): `FreshSectionHeader` (key word on the brush with sparks, optional "See all →" button), `FreshStepTile` (picture popping out of a pastel blob, optional coral step number, one-line label, two-line caption; read as "Step n, label, caption"), `FreshHelpCard` (icon badge, title, line and pill; one button) |
| `lib/src/scenery.dart` | `FreshScenery` canvas helpers (bird, cloud, hills, trawler, sea bands, palm) shared by the onboarding drawings, the home and the sign-in coast; the owner's real pictures (D287): `FreshTrawler` (the fishing trawler on its own waves) and `FreshGull` (three gull cut-outs), paths in `FreshSceneryPictures`, bundled here under `assets/scenery/` and loaded with `package: 'fresh_theme'`; `HarbourScene` (222 × 150 design box: drawn sky, sun, hills, sea and palm with the real trawler, a far-off one and four real gulls on top); `PalmsPainter` + `ScenePalm` for soft palms along a screen edge |
| `lib/src/links.dart` | `FreshLinkText` + `FreshTextPart` (D299): a sentence with tappable words; links are real text spans in the theme's teal text colour, semibold and underlined, so they wrap with the sentence, follow the text size and read as links. The app decides what a link opens |
| `lib/src/page.dart` | `FreshScrollBehavior` (clamping, no bounce, D182), `FreshCircleButton` (46 dp round button with an optional badge), `FreshPageHeader` (64 dp screen header: leading, title, actions), `FreshCard` (radius 20, D189), `FreshPictureFrame` (3:2, D193) |

### Every reusable UI part (owner rule, 2026-10-27)

Reusable parts (buttons, chips, cards, list tiles, headers, empty and loading states, sheets, price rows,
badges) live here and the app uses them from here; a screen never builds its own. Moved from `apps/mobile`:

| File | Parts |
|---|---|
| `lib/src/look.dart` | The Bold blocks look: `AppFonts` (family names), `IllustratedColors` + `illustratedColors(context)`, `BlockInk`, `AppRadius`, `cardShadow(context)`, `AppText` (display, eyebrow, price, block title), `freshColors(context)` |
| `lib/src/titles.dart` | `PageTitle` ("Your *cart*": key word on the brush with sparks), `Heading` + `HeadingLine` + `scriptStyle` (colour-block headings), `BrushSwipe` |
| `lib/src/header.dart` | `PageHeader` (the app's screen header; `PageHeader.plain` for a product name), `PaperCircleButton`, `BackCircleButton` (the app sets `BackCircleButton.defaultBack` to its router's back), `CountPip` |
| `lib/src/blocks.dart` | `BlockTone`, `SheetTitle`, `SheetNote`, `RoundIconButton`, `PageDots`, `FilterPill`, `SectionBox`, `SectionTitle`, `MenuLabel`, `SlotButton`, `SegmentedPills`, `MenuCard`, `MenuRow`, `ProgressSteps`, `StickyActionBar`, `TealActionButton`, `StickerSpot` |
| `lib/src/feedback.dart` | `SkeletonBox`, `LoadingSemantics`, `ListSkeleton`, `GridSkeleton`, `productGridDelegate`, `BannerTone` + `InfoBanner`, `showInfoSnack`, `FreshBadge`, `PrimaryButton`, `ProductPhoto` |
| `lib/src/gallery.dart` | `FreshGallery`: the one picture gallery (D266, D276). Every picture as a swipeable page that fills its parent (the screen keeps its 3:2 frame); tappable dots on a paper pill (only for more than one picture; at once under reduced motion; `dotsAlignment`); "Picture i of n" with increase / decrease and a "Show picture i" button per dot for screen readers; one picture alone, none shows `empty`. Pictures come from the app's `itemBuilder`, which passes the server's alt text |
| `lib/src/stickers.dart` | `Sticker` + `StickerTone`, `PolaroidCard`, `ReceiptRow` + `ReceiptCard` |
| `lib/src/motion.dart` | `EntranceMotion`, `motionAllowed(context)` |
| `lib/src/prices.dart` | `PriceEstimateRow` (the server's estimate and "at most" for a line), `CouponTile` (code, description, condition, one action) |

Still in the app, because they read app models, API errors or the bundled illustrations: `ColorBlockScreen`,
`AvatarCircle`, `EmptyState`, `ErrorState`, `NotFoundScreen`, `AsyncValueView`, `showErrorSnack`,
`FeatureCard`, `HelpHero`, `PriceTag`, `QuantityStepper`, `CompactStepper`, `AvailabilityBadge`,
`OrderStatusBadge`, `ProductCard`, and the home's `FoodTabBar`, `ViewCartBar` and `CategoryBubble` (being moved
with the home on its own branch). Moving them needs plain-value inputs (formatted strings, an image widget)
instead of models.

Font families are passed in by the app (the package bundles no fonts). Tests for these parts sit in
`apps/mobile/test/widgets/fresh_theme_parts_test.dart`, because CI's mobile job runs only `apps/mobile`.

Requires Flutter 3.32+ (uses `CardThemeData`). Compiled and analyzed in CI through `apps/mobile` (the
"Mobile app analyze and test" job).
