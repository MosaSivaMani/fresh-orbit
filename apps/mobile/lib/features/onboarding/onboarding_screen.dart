// Onboarding (D88, D156): three colour-block pages (coral, teal, mango) laid out like the approved preview
// docs/design/mockups/app-onboarding-rich.html (designed at 390 × 873, from the owner's reference
// onboarding-reference-owner.jpg). Each has Skip, the step eyebrow, a two-line bold heading whose second line sits
// on the yellow brush, sparks, a handwritten tagline, the character in a big ringed circle that overlaps the paper
// sheet with a sticker on its edge, and a drawn scene behind it. The sheet holds the title, one line, three feature
// tiles and a wide food banner. Page dots and the Next button stay pinned at the bottom. Sizes follow the preview
// on a tall phone and scale down on short ones; there the banner is left out, and the page scrolls rather than
// overflow when text is enlarged.

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
import '../../widgets/heading.dart';
import '../../widgets/motion.dart';
import 'onboarding_art.dart';

// Preview colours that stay the same in light and dark (like the stickers and the avatar rings).
const _brushYellow = Color(0xFFF5D04A);
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
          // Thumb zone: the dots and the big ink button never scroll away.
          ColoredBox(
            color: c.paper,
            child: SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(22, 8, 22, 12),
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

/// Sizes for one page, from the preview's 390 × 873 design scaled to the space the page really has.
class _Metrics {
  _Metrics(double width, double height)
    : scale = math.min(1.0, math.min(width / 390, height / 776)).clamp(0.62, 1.0),
      showBanner = height >= 600,
      compact = height < 600 || width < 370;

  /// 1 on the preview's phone; smaller on short or narrow ones.
  final double scale;
  final bool showBanner;
  final bool compact;

  double get avatar => 252 * scale;
  double get overlap => 41 * scale;
  double get avatarRight => 20 * scale;
  double get heading => compact ? 36.0 : 44 * math.max(scale, 0.86);
  double get tagline => 23 * math.max(scale, 0.86);
  double get bannerHeight => 150 * scale;
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
      final m = _Metrics(box.maxWidth, box.maxHeight);
      return SingleChildScrollView(
        physics: const ClampingScrollPhysics(),
        child: Column(
          children: [
            _Block(page: page, step: step, steps: steps, onSkip: onSkip, m: m),
            _Sheet(page: page, m: m),
          ],
        ),
      );
    },
  );
}

/// The colour block: Skip, eyebrow, heading with sparks, tagline, the drawn scene and room for the circle.
class _Block extends StatelessWidget {
  const _Block({required this.page, required this.step, required this.steps, required this.onSkip, required this.m});

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
    final top = math.max(MediaQuery.paddingOf(context).top, 24.0);
    final k = m.scale;
    // The circle starts 10 above the tagline row and its bottom sits [overlap] into the sheet.
    final reserved = m.avatar - m.overlap - 10 * k;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: page.tone.overlay(c),
      child: ColoredBox(
        color: page.tone.background(c),
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            Positioned.fill(
              child: ExcludeSemantics(
                child: CustomPaint(
                  painter: BlockScenePainter(scene: page.scene, dark: page.dark, soft: page.soft, scale: k),
                ),
              ),
            ),
            Padding(
              padding: EdgeInsets.fromLTRB(20, top, 8, 0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Align(
                    alignment: Alignment.centerRight,
                    child: TextButton(
                      key: const Key('onboardingSkip'),
                      style: TextButton.styleFrom(
                        foregroundColor: fg,
                        textStyle: _inter(fg, 17, FontWeight.w700),
                      ),
                      onPressed: onSkip,
                      child: const Text('Skip'),
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.only(left: 2, right: 12),
                    child: Text(
                      'Step $step of $steps'.toUpperCase(),
                      style: _inter(fg.withValues(alpha: 0.78), 12.5, FontWeight.w700, spacing: 1.5),
                    ),
                  ),
                  SizedBox(height: 6 * k),
                  Padding(
                    padding: const EdgeInsets.only(right: 48),
                    child: _BigHeading(lines: page.heading, color: fg, size: m.heading, spark: sparkColor),
                  ),
                  SizedBox(height: 6 * k),
                  SizedBox(
                    height: reserved,
                    width: double.infinity,
                    child: Stack(
                      clipBehavior: Clip.none,
                      children: [
                        Positioned(
                          left: 4,
                          top: 0,
                          width: 180 * math.max(k, 0.8),
                          height: math.min(reserved, 84.0),
                          child: _Tagline(text: page.tagline, color: taglineColor, size: m.tagline),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
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
              Text(first, style: style),
              Positioned(right: -40, top: -18, child: Spark(color: spark, scale: 1.1)),
            ],
          ),
          const SizedBox(height: 2),
          Stack(
            clipBehavior: Clip.none,
            children: [
              Transform.rotate(
                angle: -1.5 * math.pi / 180,
                child: BrushSwipe(
                  color: _brushYellow,
                  child: Text(second, style: style.copyWith(color: BlockInk.ink)),
                ),
              ),
              Positioned(right: -38, top: 12, child: Spark(color: spark, degrees: 40, scale: 0.9)),
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
      child: Transform.rotate(
        angle: -6 * math.pi / 180,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(text, style: scriptStyle(color, size / 1.12).copyWith(height: 1.12)),
            const SizedBox(height: 2),
            ExcludeSemantics(
              child: CustomPaint(size: const Size(92, 12), painter: SwooshPainter(color)),
            ),
          ],
        ),
      ),
    ),
  );
}

/// The paper sheet with the circle, sticker and sparks reaching up out of it; then title, line, tiles, banner.
class _Sheet extends StatelessWidget {
  const _Sheet({required this.page, required this.m});

  final _Page page;
  final _Metrics m;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final k = m.scale;
    final width = MediaQuery.sizeOf(context).width;
    final rise = m.avatar - m.overlap;
    final avatarLeft = width - m.avatarRight - m.avatar;
    return Stack(
      clipBehavior: Clip.none,
      children: [
        Container(
          width: double.infinity,
          decoration: BoxDecoration(
            color: c.paper,
            borderRadius: const BorderRadius.vertical(top: Radius.circular(30)),
          ),
          padding: EdgeInsets.only(top: m.overlap - 2 * k, bottom: 8),
          child: EntranceMotion(
            rise: 24,
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: Semantics(
                    header: true,
                    child: Text(
                      page.title,
                      textAlign: TextAlign.center,
                      style: _inter(c.ink, 25, FontWeight.w800, height: 1.1, spacing: -0.375),
                    ),
                  ),
                ),
                const SizedBox(height: 6),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 30),
                  child: Text(
                    page.body,
                    textAlign: TextAlign.center,
                    style: _inter(c.muted, 14.5, FontWeight.w400, height: 1.42),
                  ),
                ),
                SizedBox(height: 12 * k),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      for (var i = 0; i < page.tiles.length; i++) ...[
                        if (i > 0) const SizedBox(width: 6),
                        Expanded(
                          child: _FeatureTile(
                            tile: page.tiles[i],
                            scale: k,
                            spark: switch (i) {
                              0 => const Spark(color: _coral, degrees: -20, scale: 0.8),
                              1 => const Snowflake(color: _snowBlue),
                              _ => Spark(color: page.rightSpark, degrees: 30, scale: 0.8),
                            },
                            sparkOnLeft: i == 0,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                if (m.showBanner) ...[
                  SizedBox(height: 4 * k),
                  _FoodBanner(page: page, height: m.bannerHeight),
                ],
              ],
            ),
          ),
        ),
        const Positioned(
          left: 0,
          right: 0,
          top: 5,
          height: 40,
          child: ExcludeSemantics(child: CustomPaint(painter: SheetBirdsPainter(_sheetBird))),
        ),
        Positioned(
          top: -rise,
          right: m.avatarRight,
          child: EntranceMotion(child: _RingedAvatar(slot: page.slot, size: m.avatar)),
        ),
        Positioned(
          top: -rise + 44 * k,
          left: math.max(4.0, avatarLeft - 26 * k),
          child: ConstrainedBox(
            constraints: BoxConstraints(maxWidth: math.max(120.0, width - avatarLeft)),
            child: _Sticker(label: page.sticker, color: page.stickerColor),
          ),
        ),
        Positioned(
          top: -rise + 80 * k,
          left: math.max(0.0, avatarLeft - 42 * k),
          child: const Spark(color: _brushYellow, degrees: 200, scale: 0.85),
        ),
      ],
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

/// The sticker on the circle's edge: Inter 800 on colour, ink outline, an ink drop shadow, tilted −8°.
class _Sticker extends StatelessWidget {
  const _Sticker({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Transform.rotate(
    angle: -8 * math.pi / 180,
    child: Container(
      padding: const EdgeInsets.fromLTRB(15, 5, 15, 5),
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
/// caption. Screen readers hear "label, caption".
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
          const SizedBox(height: 6),
          Text(
            tile.label,
            textAlign: TextAlign.center,
            style: _inter(c.ink, 13.5, FontWeight.w700, height: 1.2),
          ),
          const SizedBox(height: 1),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 110),
            child: Text(
              tile.caption,
              textAlign: TextAlign.center,
              style: _inter(c.muted, 12, FontWeight.w400, height: 1.3),
            ),
          ),
        ],
      ),
    );
  }
}

/// The full-width food banner: the picture on two teal waves, the handwritten line on the right with a coral
/// underline, and a coral arrow pointing at the food. Read as one line.
class _FoodBanner extends StatelessWidget {
  const _FoodBanner({required this.page, required this.height});

  final _Page page;
  final double height;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    final k = height / 150;
    final foodHeight = 126 * k;
    return Semantics(
      key: ValueKey('banner-${page.banner.code}'),
      container: true,
      label: page.bannerLine.replaceAll('\n', ' '),
      excludeSemantics: true,
      child: SizedBox(
        height: height,
        width: double.infinity,
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
              left: MediaQuery.sizeOf(context).width * 236 / 390,
              top: 36 * k,
              child: CustomPaint(size: Size.square(40 * k), painter: const ArrowDoodlePainter(_coral)),
            ),
            Positioned(
              right: 14,
              top: 26 * k,
              width: MediaQuery.sizeOf(context).width * 0.4,
              height: height - 30 * k,
              child: Align(
                alignment: Alignment.topRight,
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  alignment: Alignment.topRight,
                  child: Transform.rotate(
                    angle: -8 * math.pi / 180,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(page.bannerLine, style: scriptStyle(c.ink, 22 / 1.12).copyWith(height: 1.08)),
                        const Padding(
                          padding: EdgeInsets.only(left: 24),
                          child: CustomPaint(size: Size(110, 8), painter: SwooshPainter(_coral, width: 2.4)),
                        ),
                      ],
                    ),
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
