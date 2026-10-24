// Onboarding (D88, D156): three colour-block pages (coral, teal, mango) laid out like the approved preview
// docs/design/mockups/app-onboarding-rich.html (designed at 390 × 873, from the owner's reference
// onboarding-reference-owner.jpg). Each has Skip, the step eyebrow, a two-line bold heading whose second line sits
// on the yellow brush, sparks, a handwritten tagline, the character in a big ringed circle that overlaps the paper
// sheet with a sticker on its edge, and a drawn scene behind it. The sheet holds the title, one line, three feature
// tiles and a wide food banner. Page dots and the Next button stay pinned at the bottom. Sizes follow the preview
// on a tall phone and scale down on short ones; there the banner is left out. A page never scrolls: it shrinks to
// fit above the button, and only very large system text (1.5× and up) lets it scroll (D182).

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../illustrations/illustration.dart';
import '../../state/core_providers.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/motion.dart';
import 'onboarding_art.dart';

// Preview colours that stay the same in light and dark (like the stickers and the avatar rings).
const _brushYellow = FreshHighlightColors.brush;
const _coral = Color(0xFFF07A5A);
const _teal = Color(0xFF0B7D70);
const _snowBlue = Color(0xFF3FA7C9);
const _sheetBird = Color(0xFF8FB3AE);

enum _Tint { green, blue, peach }

class _Tile {
  const _Tile(this.slot, this.label, this.caption, this.icon, this.tint);

  final IllustrationSlot slot;
  final String label;
  final String caption;

  /// Drawn on the blob until the tile's picture is bundled.
  final IconData icon;
  final _Tint tint;
}

class _Page {
  const _Page({
    required this.tone,
    required this.scene,
    required this.slot,
    required this.heading,
    required this.tagline,
    required this.sticker,
    required this.stickerColor,
    required this.dark,
    required this.soft,
    required this.title,
    required this.body,
    required this.tiles,
    required this.rightSpark,
    required this.banner,
    required this.bannerLine,
  });

  final BlockTone tone;
  final OnboardingScene scene;
  final IllustrationSlot slot;

  /// The plain line and the brush line.
  final (String, String) heading;

  /// Handwritten, two lines.
  final String tagline;
  final String sticker;
  final Color stickerColor;

  /// The block's darker shade (silhouettes) and the pale cloud tint.
  final Color dark;
  final Color soft;
  final String title;
  final String body;
  final List<_Tile> tiles;

  /// The spark on the right tile's corner.
  final Color rightSpark;
  final IllustrationSlot banner;

  /// Handwritten, line breaks as in the preview.
  final String bannerLine;
}

const _pages = [
  _Page(
    tone: BlockTone.coral,
    scene: OnboardingScene.fresh,
    slot: IllustrationSlot.onboardingFresh,
    heading: ('Straight off', 'the boats'),
    tagline: 'Freshness you\ncan taste',
    sticker: 'Morning catch',
    stickerColor: _brushYellow,
    dark: Color(0xFFC65A3F),
    soft: Color(0x8CFFE8D6),
    title: 'Hours old, not days',
    body: 'Fish, prawns and crabs from the morning catch, kept cold from our store to your door.',
    tiles: [
      _Tile(
        IllustrationSlot.onboardingTileMorningCatch,
        'Morning catch',
        'Direct from local boats',
        Icons.set_meal_rounded,
        _Tint.green,
      ),
      _Tile(IllustrationSlot.onboardingTileKeptChilled, 'Kept chilled', 'On ice all the way', Icons.ac_unit_rounded, _Tint.blue),
      _Tile(
        IllustrationSlot.onboardingTileDeliveredFresh,
        'Delivered fresh',
        'From our store to your door',
        Icons.local_shipping_rounded,
        _Tint.peach,
      ),
    ],
    rightSpark: _coral,
    banner: IllustrationSlot.onboardingBannerFresh,
    bannerLine: "From\ntoday's catch\nto your kitchen",
  ),
  _Page(
    tone: BlockTone.teal,
    scene: OnboardingScene.cleaned,
    slot: IllustrationSlot.onboardingCleaned,
    heading: ('Cleaned', 'your way'),
    tagline: 'Just the way\nyou cook it',
    sticker: 'Your cut',
    stickerColor: _coral,
    dark: Color(0xFF075E54),
    soft: Color(0x59BFE3E0),
    title: 'Steaks, curry cut, fillet',
    body: 'Pick the cut. We weigh it after cleaning, so you pay for what you cook, never for the waste.',
    tiles: [
      _Tile(
        IllustrationSlot.onboardingTileYourCut,
        'Your cut',
        'Whole, steaks, curry or fillet',
        Icons.restaurant_rounded,
        _Tint.peach,
      ),
      _Tile(
        IllustrationSlot.onboardingTileCleanedByHand,
        'Cleaned by hand',
        'Scaled, gutted, washed',
        Icons.water_drop_rounded,
        _Tint.blue,
      ),
      _Tile(
        IllustrationSlot.onboardingTileNetWeight,
        'Pay net weight',
        'Weighed after cleaning',
        Icons.scale_rounded,
        _Tint.green,
      ),
    ],
    rightSpark: _teal,
    banner: IllustrationSlot.onboardingBannerCleaned,
    bannerLine: 'Ready\nfor your pan',
  ),
  _Page(
    tone: BlockTone.mango,
    scene: OnboardingScene.delivery,
    slot: IllustrationSlot.onboardingDelivery,
    heading: ('At your', 'door'),
    tagline: 'In the slot\nyou pick',
    sticker: 'Kept cold',
    stickerColor: Color(0xFFBFE3EE),
    dark: Color(0xFFCF8E22),
    soft: Color(0x8CFFF6DE),
    title: 'Pick a delivery slot',
    body: 'Your order comes in a cold box. Pay by cash or UPI when it arrives.',
    tiles: [
      _Tile(IllustrationSlot.onboardingTilePickSlot, 'Pick a slot', 'Morning or evening', Icons.schedule_rounded, _Tint.peach),
      _Tile(IllustrationSlot.onboardingTileColdBox, 'Cold box', 'Packed on ice', Icons.inventory_2_rounded, _Tint.blue),
      _Tile(IllustrationSlot.onboardingTileCashUpi, 'Cash or UPI', 'Pay when it arrives', Icons.payments_rounded, _Tint.green),
    ],
    rightSpark: _teal,
    banner: IllustrationSlot.onboardingBannerDelivery,
    bannerLine: 'From our store\nto your door',
  ),
];

TextStyle _inter(Color color, double size, FontWeight weight, {double? height, double? spacing}) => TextStyle(
  fontFamily: AppFonts.body,
  fontFamilyFallback: FreshFonts.sansFallback,
  fontWeight: weight,
  fontSize: size,
  height: height,
  letterSpacing: spacing,
  color: color,
);

class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key});

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  final _controller = PageController();
  int _index = 0;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _finish() async {
    await ref.read(appPrefsProvider).setOnboardingDone();
    if (mounted) context.go('/location');
  }

  Future<void> _next() async {
    if (_index == _pages.length - 1) {
      await _finish();
    } else if (motionAllowed(context)) {
      await _controller.nextPage(duration: FreshDurations.slow, curve: Curves.easeOutCubic);
    } else {
      _controller.jumpToPage(_index + 1);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final last = _index == _pages.length - 1;
    return Scaffold(
      backgroundColor: c.paper,
      body: Column(
        children: [
          Expanded(
            child: PageView.builder(
              controller: _controller,
              itemCount: _pages.length,
              onPageChanged: (i) => setState(() => _index = i),
              itemBuilder: (context, i) =>
                  _OnboardingPage(page: _pages[i], step: i + 1, steps: _pages.length, onSkip: _finish),
            ),
          ),
          // Thumb zone: the dots and the big ink button never scroll away. Dots 12 above the 54 dp button, 12 below
          // it plus the safe area, 16 dp gutters.
          ColoredBox(
            color: c.paper,
            child: SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    _Dots(count: _pages.length, index: _index),
                    const SizedBox(height: 12),
                    SizedBox(
                      width: double.infinity,
                      child: FilledButton(
                        key: const Key('onboardingNext'),
                        style: FilledButton.styleFrom(
                          minimumSize: const Size.fromHeight(54),
                          shape: const StadiumBorder(),
                          textStyle: const TextStyle(
                            fontFamily: AppFonts.body,
                            fontFamilyFallback: FreshFonts.sansFallback,
                            fontWeight: FontWeight.w700,
                            fontSize: 18,
                          ),
                        ),
                        onPressed: _next,
                        child: Text(last ? 'Get started →' : 'Next →'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Page dots: 8 px dots, the current one a 26 px ink pill.
class _Dots extends StatelessWidget {
  const _Dots({required this.count, required this.index});

  final int count;
  final int index;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final idle = Theme.of(context).brightness == Brightness.dark ? c.line : const Color(0xFFDCCFBB);
    return Semantics(
      label: 'Page ${index + 1} of $count',
      child: ExcludeSemantics(
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            for (var i = 0; i < count; i++)
              Container(
                margin: const EdgeInsets.symmetric(horizontal: 3),
                width: i == index ? 26 : 8,
                height: 8,
                decoration: BoxDecoration(
                  color: i == index ? c.ink : idle,
                  borderRadius: BorderRadius.circular(FreshRadius.full),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// System text scale from which a page scrolls instead of being laid out to the screen (D182).
const double _scrollTextScale = 1.5;

/// The sheet's top radius, and how far the circle reaches down into the sheet.
const double _sheetRadius = 28;
const double _overlap = 28;

/// Side gutters: 20 for the text and the tiles, 16 for the banner (and the button).
const double _textGutter = 20;
const double _bannerGutter = 16;

/// The food banner's smallest and largest height; with less room than the smallest it is left out.
const double _bannerMin = 96;
const double _bannerMax = 150;

/// Sizes for one page, laid out to the space the page really has: the colour block takes about 45% of the
/// height (300–400), the circle is 212 on a 390-wide phone and shrinks with the block, and the sheet takes the
/// rest. Everything spans the full width.
class _Metrics {
  _Metrics({required this.width, required this.height, required this.topInset})
    : block = math.min((height * 0.45).clamp(300.0, 400.0), height * 0.62);

  final double width;
  final double height;
  final double topInset;
  final double block;

  /// 1 on a 390-wide phone, smaller on narrower ones.
  double get k => math.min(1.0, width / 390);
  double get avatar => math.min(width * 0.56, (212 * block / 330).clamp(150.0, 212.0));
  double get circleCenter => width * 0.62;
  double get circleLeft => circleCenter - avatar / 2;
  double get circleTop => block + _overlap - avatar;
  double get heading => math.max(34.0, 40 * width / 390);
  double get headingHeight => 2 * heading * 1.04 + 2;

  /// The eyebrow and Skip row (Skip keeps a 48 dp target).
  static const double topRow = 48;
  double get headingTop => topInset + topRow + 8;
  double get taglineTop => headingTop + headingHeight + 8;
  double get taglineHeight => 56 * k;
  double get taglineWidth => math.max(96.0, circleLeft - _textGutter - 4);
  double get sceneTop => taglineTop + taglineHeight;
  double get sheet => math.max(0.0, height - block);
}

class _OnboardingPage extends StatelessWidget {
  const _OnboardingPage({required this.page, required this.step, required this.steps, required this.onSkip});

  final _Page page;
  final int step;
  final int steps;
  final VoidCallback onSkip;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, box) {
      final m = _Metrics(
        width: box.maxWidth,
        height: box.maxHeight,
        topInset: math.max(MediaQuery.paddingOf(context).top, 24.0),
      );
      // Only very large system text may scroll; otherwise the page is laid out to the screen and stands still
      // (owner, D182).
      if (MediaQuery.textScalerOf(context).scale(10) / 10 >= _scrollTextScale) {
        return SingleChildScrollView(
          physics: const ClampingScrollPhysics(),
          child: _PageBody(page: page, step: step, steps: steps, onSkip: onSkip, m: m, fixed: false),
        );
      }
      return SizedBox(
        height: box.maxHeight,
        child: _PageBody(page: page, step: step, steps: steps, onSkip: onSkip, m: m, fixed: true),
      );
    },
  );
}

/// The block and the sheet in one column, the block colour running on under the sheet's rounded top, and the
/// circle, sticker and spark on top of both.
class _PageBody extends StatelessWidget {
  const _PageBody({
    required this.page,
    required this.step,
    required this.steps,
    required this.onSkip,
    required this.m,
    required this.fixed,
  });

  final _Page page;
  final int step;
  final int steps;
  final VoidCallback onSkip;
  final _Metrics m;

  /// True: the sheet takes exactly the rest of the height. False (very large text): natural height, scrolling.
  final bool fixed;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final stickerTop = m.circleTop + m.avatar * 0.64;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: page.tone.overlay(c),
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          // The block colour continues behind the sheet's rounded corners, so the sheet's top is a clean curve.
          Positioned(
            left: 0,
            right: 0,
            top: 0,
            height: m.block + _sheetRadius,
            child: ColoredBox(color: page.tone.background(c)),
          ),
          Column(
            children: [
              _Block(
                key: ValueKey('onboardingBlock-$step'),
                page: page,
                step: step,
                steps: steps,
                onSkip: onSkip,
                m: m,
              ),
              _Sheet(key: ValueKey('onboardingSheet-$step'), page: page, m: m, fixed: fixed),
            ],
          ),
          Positioned(
            left: m.circleLeft,
            top: m.circleTop,
            child: EntranceMotion(child: _RingedAvatar(slot: page.slot, size: m.avatar)),
          ),
          // The sticker sits on the circle's lower-left edge, over the apron and the catch, never over a face.
          Positioned(
            left: math.max(8.0, m.circleLeft - 34),
            top: stickerTop,
            child: ConstrainedBox(
              constraints: BoxConstraints(maxWidth: math.max(120.0, m.avatar * 0.9)),
              child: _Sticker(key: const Key('onboardingSticker'), label: page.sticker, color: page.stickerColor),
            ),
          ),
          Positioned(
            left: math.max(0.0, m.circleLeft - 44),
            top: stickerTop - 24,
            child: const Sparks(color: _brushYellow, degrees: 200, scale: 0.7),
          ),
        ],
      ),
    );
  }
}

/// The colour block: the eyebrow and Skip, the heading with sparks, the tagline and the drawn scene.
class _Block extends StatelessWidget {
  const _Block({
    super.key,
    required this.page,
    required this.step,
    required this.steps,
    required this.onSkip,
    required this.m,
  });

  final _Page page;
  final int step;
  final int steps;
  final VoidCallback onSkip;
  final _Metrics m;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final fg = page.tone.foreground(c);
    final teal = page.tone == BlockTone.teal;
    // Paper text on coral and teal, ink on mango; sparks cream, yellow on teal.
    final taglineColor = page.tone == BlockTone.mango ? BlockInk.ink : (teal ? fg : BlockInk.paper);
    final sparkColor = teal ? _brushYellow : BlockInk.paper;
    return SizedBox(
      height: m.block,
      width: double.infinity,
      child: Stack(
        children: [
          Positioned.fill(
            child: ExcludeSemantics(
              child: CustomPaint(
                painter: BlockScenePainter(
                  scene: page.scene,
                  dark: page.dark,
                  soft: page.soft,
                  scale: m.k,
                  circleLeft: m.circleLeft,
                  circleCenter: m.circleCenter,
                  sceneTop: m.sceneTop,
                ),
              ),
            ),
          ),
          Positioned(
            left: _textGutter,
            right: 8,
            top: m.topInset,
            height: _Metrics.topRow,
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    'Step $step of $steps'.toUpperCase(),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: _inter(fg.withValues(alpha: 0.78), 12.5, FontWeight.w700, spacing: 1.5),
                  ),
                ),
                TextButton(
                  key: const Key('onboardingSkip'),
                  style: TextButton.styleFrom(
                    foregroundColor: fg,
                    textStyle: _inter(fg, 17, FontWeight.w700),
                  ),
                  onPressed: onSkip,
                  child: const Text('Skip'),
                ),
              ],
            ),
          ),
          Positioned(
            left: _textGutter,
            right: 48,
            top: m.headingTop,
            height: m.headingHeight,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.topLeft,
              child: _BigHeading(lines: page.heading, color: fg, size: m.heading, spark: sparkColor),
            ),
          ),
          Positioned(
            left: _textGutter,
            top: m.taglineTop,
            width: m.taglineWidth,
            height: m.taglineHeight,
            child: _Tagline(text: page.tagline, color: taglineColor, size: 23 * math.max(m.k, 0.86)),
          ),
        ],
      ),
    );
  }
}

/// Two heavy lines, the second on the yellow brush, with a spark after each. One header for screen readers.
class _BigHeading extends StatelessWidget {
  const _BigHeading({required this.lines, required this.color, required this.size, required this.spark});

  final (String, String) lines;
  final Color color;
  final double size;
  final Color spark;

  @override
  Widget build(BuildContext context) {
    final style = AppText.display(color, size, height: 1.04);
    final (first, second) = lines;
    return Semantics(
      header: true,
      label: '$first $second',
      excludeSemantics: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Stack(
            clipBehavior: Clip.none,
            children: [
              Text(first, maxLines: 1, style: style),
              Positioned(right: -36, top: -14, child: Sparks(color: spark)),
            ],
          ),
          const SizedBox(height: 2),
          Stack(
            clipBehavior: Clip.none,
            children: [
              BrushHighlight(
                degrees: -1.5,
                child: Text(second, maxLines: 1, style: style.copyWith(color: BlockInk.ink)),
              ),
              Positioned(right: -34, top: 10, child: Sparks(color: spark, degrees: 40, scale: 0.85)),
            ],
          ),
        ],
      ),
    );
  }
}

/// The handwritten tagline, tilted −6°, with an underline swoosh in the same colour. Shrinks rather than overflow.
class _Tagline extends StatelessWidget {
  const _Tagline({required this.text, required this.color, required this.size});

  final String text;
  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) => Align(
    alignment: Alignment.topLeft,
    child: FittedBox(
      fit: BoxFit.scaleDown,
      alignment: Alignment.topLeft,
      child: ScriptLine(
        text: text,
        fontFamily: AppFonts.script,
        fontFamilyFallback: FreshFonts.sansFallback,
        color: color,
        fontSize: size,
        height: 1.12,
        degrees: -6,
        rotationAlignment: Alignment.center,
        swooshGap: 2,
      ),
    ),
  );
}

/// The paper sheet (28 radius top): title, line and tiles, then the food banner in the room that is left.
class _Sheet extends StatelessWidget {
  const _Sheet({super.key, required this.page, required this.m, required this.fixed});

  final _Page page;
  final _Metrics m;
  final bool fixed;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final top = EntranceMotion(rise: 24, child: _SheetTop(page: page, m: m));
    final Widget body = fixed
        ? CustomMultiChildLayout(
            delegate: const _SheetLayout(),
            children: [
              // Scaled down (never narrowed) only if even the text and tiles do not fit.
              LayoutId(id: _SheetPart.top, child: FreshFitWidth(child: top)),
              LayoutId(id: _SheetPart.banner, child: _FoodBanner(page: page)),
            ],
          )
        : Column(
            children: [
              top,
              const SizedBox(height: 12),
              SizedBox(height: _bannerMax * m.k, child: _FoodBanner(page: page)),
              const SizedBox(height: 12),
            ],
          );
    return Container(
      width: double.infinity,
      height: fixed ? m.sheet : null,
      decoration: BoxDecoration(
        color: c.paper,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(_sheetRadius)),
      ),
      child: Stack(
        children: [
          const Positioned(
            left: 0,
            right: 0,
            top: 5,
            height: 40,
            child: ExcludeSemantics(child: CustomPaint(painter: SheetBirdsPainter(_sheetBird))),
          ),
          body,
        ],
      ),
    );
  }
}

enum _SheetPart { top, banner }

/// The text and tiles at their natural height, then the banner in the room that is left (96–150, or none), with
/// the spare room split above and below it.
class _SheetLayout extends MultiChildLayoutDelegate {
  const _SheetLayout();

  static const double _gap = 12;
  static const double _bottom = 12;

  @override
  void performLayout(Size size) {
    final top = layoutChild(
      _SheetPart.top,
      BoxConstraints(minWidth: size.width, maxWidth: size.width, maxHeight: math.max(0.0, size.height - _bottom)),
    );
    positionChild(_SheetPart.top, Offset.zero);
    final room = math.max(0.0, size.height - top.height - _gap - _bottom);
    final banner = room >= _bannerMin ? math.min(room, _bannerMax) : 0.0;
    layoutChild(_SheetPart.banner, BoxConstraints.tightFor(width: size.width, height: banner));
    positionChild(_SheetPart.banner, Offset(0, top.height + _gap + (room - banner) / 2));
  }

  @override
  bool shouldRelayout(_SheetLayout oldDelegate) => false;
}

/// Title, line and the three tiles, inside 20 dp gutters; the title starts below the circle's overlap.
class _SheetTop extends StatelessWidget {
  const _SheetTop({required this.page, required this.m});

  final _Page page;
  final _Metrics m;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(_textGutter, _overlap + 8, _textGutter, 0),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Semantics(
            header: true,
            child: Text(
              page.title,
              textAlign: TextAlign.center,
              style: _inter(c.ink, 22, FontWeight.w800, height: 1.15, spacing: -0.3),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            page.body,
            textAlign: TextAlign.center,
            style: _inter(c.muted, 14.5, FontWeight.w400, height: 1.4),
          ),
          const SizedBox(height: 16),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (var i = 0; i < page.tiles.length; i++) ...[
                if (i > 0) const SizedBox(width: 8),
                Expanded(
                  child: _FeatureTile(
                    tile: page.tiles[i],
                    scale: m.k,
                    spark: switch (i) {
                      0 => const Sparks(color: _coral, degrees: -20, scale: 0.8),
                      1 => const Snowflake(color: _snowBlue),
                      _ => Sparks(color: page.rightSpark, degrees: 30, scale: 0.8),
                    },
                    sparkOnLeft: i == 0,
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

/// The character in a big circle: a 7 px ink ring inside a 6 px paper ring.
class _RingedAvatar extends StatelessWidget {
  const _RingedAvatar({required this.slot, required this.size});

  final IllustrationSlot slot;
  final double size;

  @override
  Widget build(BuildContext context) => Container(
    key: ValueKey('avatar-${slot.code}'),
    width: size,
    height: size,
    padding: const EdgeInsets.all(6),
    decoration: const BoxDecoration(color: BlockInk.paper, shape: BoxShape.circle),
    child: Container(
      padding: const EdgeInsets.all(7),
      decoration: const BoxDecoration(color: BlockInk.ink, shape: BoxShape.circle),
      child: ClipOval(
        child: ColoredBox(
          color: BlockInk.paper,
          child: Illustration(slot: slot, size: size - 26, inCircle: true, focus: avatarFocus(slot), excludeFromSemantics: true),
        ),
      ),
    ),
  );
}

/// The sticker on the circle's lower-left edge: Inter 800 on colour, ink outline, an ink drop shadow, tilted −8°.
class _Sticker extends StatelessWidget {
  const _Sticker({super.key, required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Transform.rotate(
    angle: -8 * math.pi / 180,
    child: Container(
      padding: const EdgeInsets.fromLTRB(14, 4, 14, 4),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(FreshRadius.full),
        border: Border.all(color: BlockInk.ink, width: 2.5),
        boxShadow: const [BoxShadow(color: BlockInk.ink, offset: Offset(0, 3))],
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: _inter(BlockInk.ink, 14, FontWeight.w800),
      ),
    ),
  );
}

/// One feature tile: the picture popping out of a pastel blob, a spark on its corner, a bold label and a muted
/// caption (two lines at most each), top-aligned with its neighbours. Screen readers hear "label, caption".
class _FeatureTile extends StatelessWidget {
  const _FeatureTile({required this.tile, required this.scale, required this.spark, required this.sparkOnLeft});

  final _Tile tile;
  final double scale;
  final Widget spark;
  final bool sparkOnLeft;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    final tint = switch (tile.tint) {
      _Tint.green => dark ? c.tealSoft : const Color(0xFFE2F1DD),
      _Tint.blue => dark ? c.iceSoft : const Color(0xFFDDF1F7),
      _Tint.peach => dark ? c.coralSoft : const Color(0xFFFBE6DC),
    };
    final k = math.max(scale, 0.8);
    final blobW = 98 * k;
    final blobH = 66 * k;
    final popUp = 24 * k;
    return Semantics(
      key: ValueKey('tile-${tile.slot.code}'),
      container: true,
      label: '${tile.label}, ${tile.caption}',
      excludeSemantics: true,
      child: Column(
        children: [
          SizedBox(
            height: blobH + popUp,
            child: Stack(
              clipBehavior: Clip.none,
              alignment: Alignment.topCenter,
              children: [
                Positioned(
                  top: popUp,
                  child: Container(
                    width: blobW,
                    height: blobH,
                    decoration: BoxDecoration(
                      color: tint,
                      borderRadius: BorderRadius.only(
                        topLeft: Radius.elliptical(blobW * 0.46, blobH * 0.55),
                        topRight: Radius.elliptical(blobW * 0.54, blobH * 0.5),
                        bottomRight: Radius.elliptical(blobW * 0.5, blobH * 0.5),
                        bottomLeft: Radius.elliptical(blobW * 0.5, blobH * 0.45),
                      ),
                    ),
                  ),
                ),
                Positioned(
                  top: 0,
                  child: Illustration(
                    slot: tile.slot,
                    size: 96 * k,
                    height: 84 * k,
                    excludeFromSemantics: true,
                    fallback: Padding(
                      padding: EdgeInsets.only(top: popUp),
                      child: Center(
                        child: Icon(
                          tile.icon,
                          key: ValueKey('tile-fallback-${tile.slot.code}'),
                          size: 34 * k,
                          color: c.tealDeep,
                        ),
                      ),
                    ),
                  ),
                ),
                Positioned(
                  top: popUp - 14,
                  left: sparkOnLeft ? -4 : null,
                  right: sparkOnLeft ? null : -4,
                  child: spark,
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          Text(
            tile.label,
            textAlign: TextAlign.center,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: _inter(c.ink, 13.5, FontWeight.w600, height: 1.2),
          ),
          const SizedBox(height: 2),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 110),
            child: Text(
              tile.caption,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: _inter(c.muted, 12, FontWeight.w400, height: 1.3),
            ),
          ),
        ],
      ),
    );
  }
}

/// The food banner inside 16 dp gutters, radius 20, as tall as the room it is given (left out under 96).
class _FoodBanner extends StatelessWidget {
  const _FoodBanner({required this.page});

  final _Page page;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, box) {
      if (!box.maxHeight.isFinite || box.maxHeight < _bannerMin) return const SizedBox.shrink();
      return Padding(
        padding: const EdgeInsets.symmetric(horizontal: _bannerGutter),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(20),
          child: _BannerArt(
            page: page,
            width: math.max(0.0, box.maxWidth - 2 * _bannerGutter),
            height: math.min(box.maxHeight, _bannerMax),
          ),
        ),
      );
    },
  );
}

/// The banner itself: the picture on two teal waves, the handwritten line on the right with a coral underline,
/// and a coral arrow pointing at the food. Read as one line.
class _BannerArt extends StatelessWidget {
  const _BannerArt({required this.page, required this.width, required this.height});

  final _Page page;
  final double width;
  final double height;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    final k = height / _bannerMax;
    final foodHeight = 126 * k;
    return Semantics(
      key: ValueKey('banner-${page.banner.code}'),
      container: true,
      label: page.bannerLine.replaceAll('\n', ' '),
      excludeSemantics: true,
      child: SizedBox(
        height: height,
        width: width,
        child: Stack(
          clipBehavior: Clip.hardEdge,
          children: [
            Positioned.fill(
              child: CustomPaint(
                painter: BannerWavePainter(
                  light: dark ? c.tealSoft.withValues(alpha: 0.6) : const Color(0xFFDDF0EC),
                  mid: dark ? c.tealSoft : const Color(0xFFBFE3E0),
                  dots: const Color(0xFF9ED3CC),
                  line: c.paper,
                ),
              ),
            ),
            Positioned(
              left: -6,
              bottom: 6 * k,
              child: Illustration(
                slot: page.banner,
                size: foodHeight * 2,
                height: foodHeight,
                excludeFromSemantics: true,
                fallback: CustomPaint(
                  key: ValueKey('banner-fallback-${page.banner.code}'),
                  size: Size(foodHeight * 2, foodHeight),
                  painter: BannerFoodPainter(scene: page.scene, coral: c.coral, teal: c.tealText),
                ),
              ),
            ),
            Positioned(
              left: width * 0.58,
              top: 30 * k,
              child: CustomPaint(size: Size.square(36 * k), painter: const ArrowDoodlePainter(_coral)),
            ),
            Positioned(
              right: 12,
              top: 20 * k,
              width: width * 0.4,
              height: height - 26 * k,
              child: Align(
                alignment: Alignment.topRight,
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  alignment: Alignment.topRight,
                  child: ScriptLine(
                    text: page.bannerLine,
                    fontFamily: AppFonts.script,
                    fontFamilyFallback: FreshFonts.sansFallback,
                    color: c.ink,
                    fontSize: 22,
                    height: 1.08,
                    degrees: -8,
                    rotationAlignment: Alignment.center,
                    swooshColor: _coral,
                    swooshSize: const Size(110, 8),
                    swooshWidth: 2.4,
                    swooshIndent: 24,
                    swooshGap: 0,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
