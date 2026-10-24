// Onboarding (D88, owner's reference docs/design/mockups/onboarding-reference-owner.jpg): three colour-block pages
// (coral, teal, mango). Each has Skip, the step eyebrow, a two-line bold heading with the second line on the brush,
// a handwritten tagline, the character in the big ringed circle overlapping the paper sheet with a sticker on its
// edge, and a drawn scene behind it. The sheet holds the title, one line, three feature tiles and a wide food
// banner. Page dots and the Next button stay pinned at the bottom. Short screens get a smaller circle and no
// banner; the page scrolls rather than overflow.

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
import '../../widgets/illustrated.dart';
import '../../widgets/motion.dart';
import 'onboarding_art.dart';

enum _Tint { teal, ice, coral, mango }

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
    required this.stickerTone,
    required this.title,
    required this.body,
    required this.tiles,
    required this.banner,
    required this.bannerLines,
  });

  final BlockTone tone;
  final OnboardingScene scene;
  final IllustrationSlot slot;
  final List<HeadingLine> heading;
  final String tagline;
  final String sticker;
  final StickerTone stickerTone;
  final String title;
  final String body;
  final List<_Tile> tiles;
  final IllustrationSlot banner;
  final (String, String) bannerLines;
}

const _pages = [
  _Page(
    tone: BlockTone.coral,
    scene: OnboardingScene.fresh,
    slot: IllustrationSlot.onboardingFresh,
    heading: [HeadingLine('Straight off'), HeadingLine('the boats', brush: true)],
    tagline: 'Freshness you can taste',
    sticker: 'Morning catch',
    stickerTone: StickerTone.mango,
    title: 'Hours old, not days',
    body: 'Fish, prawns and crabs from the morning catch, kept cold from our store to your door.',
    tiles: [
      _Tile(
        IllustrationSlot.onboardingTileMorningCatch,
        'Morning catch',
        'Direct from local boats',
        Icons.set_meal_rounded,
        _Tint.teal,
      ),
      _Tile(IllustrationSlot.onboardingTileKeptChilled, 'Kept chilled', 'On ice all the way', Icons.ac_unit_rounded, _Tint.ice),
      _Tile(
        IllustrationSlot.onboardingTileDeliveredFresh,
        'Delivered fresh',
        'From our store to your door',
        Icons.local_shipping_rounded,
        _Tint.coral,
      ),
    ],
    banner: IllustrationSlot.onboardingBannerFresh,
    bannerLines: ("From today's catch", 'to your kitchen'),
  ),
  _Page(
    tone: BlockTone.teal,
    scene: OnboardingScene.cleaned,
    slot: IllustrationSlot.onboardingCleaned,
    heading: [HeadingLine('Cleaned'), HeadingLine('your way', brush: true)],
    tagline: 'Just the way you cook it',
    sticker: 'Your cut',
    stickerTone: StickerTone.coral,
    title: 'Steaks, curry cut, fillet',
    body: 'Pick the cut. We weigh it after cleaning, so you pay for what you cook, never for the waste.',
    tiles: [
      _Tile(
        IllustrationSlot.onboardingTileYourCut,
        'Your cut',
        'Whole, steaks, curry or fillet',
        Icons.restaurant_rounded,
        _Tint.coral,
      ),
      _Tile(
        IllustrationSlot.onboardingTileCleanedByHand,
        'Cleaned by hand',
        'Scaled, gutted, washed',
        Icons.water_drop_rounded,
        _Tint.ice,
      ),
      _Tile(
        IllustrationSlot.onboardingTileNetWeight,
        'Pay net weight',
        'Weighed after cleaning',
        Icons.scale_rounded,
        _Tint.mango,
      ),
    ],
    banner: IllustrationSlot.onboardingBannerCleaned,
    bannerLines: ('Ready for', 'your pan'),
  ),
  _Page(
    tone: BlockTone.mango,
    scene: OnboardingScene.delivery,
    slot: IllustrationSlot.onboardingDelivery,
    heading: [HeadingLine('At your'), HeadingLine('door', brush: true)],
    tagline: 'In the slot you pick',
    sticker: 'Kept cold',
    stickerTone: StickerTone.teal,
    title: 'Pick a delivery slot',
    body: 'Your order comes in a cold box. Pay by cash or UPI when it arrives.',
    tiles: [
      _Tile(IllustrationSlot.onboardingTilePickSlot, 'Pick a slot', 'Morning or evening', Icons.schedule_rounded, _Tint.coral),
      _Tile(IllustrationSlot.onboardingTileColdBox, 'Cold box', 'Packed on ice', Icons.inventory_2_rounded, _Tint.ice),
      _Tile(IllustrationSlot.onboardingTileCashUpi, 'Cash or UPI', 'Pay when it arrives', Icons.payments_rounded, _Tint.teal),
    ],
    banner: IllustrationSlot.onboardingBannerDelivery,
    bannerLines: ('From our store', 'to your door'),
  ),
];

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
              itemBuilder: (context, i) => _OnboardingPage(
                page: _pages[i],
                step: i + 1,
                steps: _pages.length,
                onSkip: _finish,
              ),
            ),
          ),
          // Thumb zone: the dots and the big ink button never scroll away.
          ColoredBox(
            color: c.paper,
            child: SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(20, 10, 20, 14),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    PageDots(count: _pages.length, index: _index),
                    const SizedBox(height: 14),
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
                            fontSize: 16,
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

class _OnboardingPage extends StatelessWidget {
  const _OnboardingPage({required this.page, required this.step, required this.steps, required this.onSkip});

  final _Page page;
  final int step;
  final int steps;
  final VoidCallback onSkip;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, box) {
      final c = illustratedColors(context);
      final width = box.maxWidth;
      final height = box.maxHeight;
      // Short screens: a smaller circle and no banner, so the story still fits before the page has to scroll.
      final avatar = math.min(height * 0.3, width * 0.58).clamp(128.0, 236.0);
      final overlap = avatar * 0.18;
      final showBanner = height >= 600;
      final bannerHeight = (height * 0.17).clamp(100.0, 136.0);
      final compact = width < 370 || height < 600;
      final block = page.tone.background(c);
      final wave = page.tone == BlockTone.teal ? BlockInk.paper.withValues(alpha: 0.26) : const Color(0xCC8FD0C6);
      return SingleChildScrollView(
        physics: const ClampingScrollPhysics(),
        child: Column(
          children: [
            _Block(page: page, step: step, steps: steps, onSkip: onSkip, avatar: avatar, overlap: overlap, wave: wave, compact: compact),
            ColoredBox(
              // The sheet's rounded corners show the wave strip's colour.
              color: Color.alphaBlend(wave, block),
              child: _Sheet(
                page: page,
                avatar: avatar,
                overlap: overlap,
                banner: showBanner ? bannerHeight : null,
              ),
            ),
          ],
        ),
      );
    },
  );
}

/// The colour block: Skip, eyebrow, heading, tagline, the drawn scene and room for the circle on the right.
class _Block extends StatelessWidget {
  const _Block({
    required this.page,
    required this.step,
    required this.steps,
    required this.onSkip,
    required this.avatar,
    required this.overlap,
    required this.wave,
    required this.compact,
  });

  final _Page page;
  final int step;
  final int steps;
  final VoidCallback onSkip;
  final double avatar;
  final double overlap;
  final Color wave;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final block = page.tone.background(c);
    final fg = page.tone.foreground(c);
    final mango = page.tone == BlockTone.mango;
    // The tagline is paper on coral and teal (dark ink on the brighter dark-mode teal), ink on mango.
    final tagline = mango ? BlockInk.ink : (page.tone == BlockTone.teal ? fg : BlockInk.paper);
    final shade = blockShade(block, amount: page.tone == BlockTone.teal ? 0.36 : 0.3);
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: page.tone.overlay(c),
      child: ColoredBox(
        color: block,
        child: Stack(
          children: [
            Positioned.fill(
              child: ExcludeSemantics(
                child: CustomPaint(
                  painter: BlockScenePainter(
                    scene: page.scene,
                    shade: shade,
                    light: BlockInk.paper.withValues(alpha: page.tone == BlockTone.teal ? 0.3 : 0.32),
                    wave: wave,
                    glow: (mango ? BlockInk.paper : c.mango).withValues(alpha: 0.75),
                    avatar: avatar,
                    overlap: overlap,
                  ),
                ),
              ),
            ),
            Padding(
              padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 2, 8, 0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Align(
                    alignment: Alignment.centerRight,
                    child: TextButton(
                      key: const Key('onboardingSkip'),
                      style: TextButton.styleFrom(
                        foregroundColor: fg,
                        textStyle: const TextStyle(
                          fontFamily: AppFonts.body,
                          fontFamilyFallback: FreshFonts.sansFallback,
                          fontWeight: FontWeight.w700,
                          fontSize: 15,
                        ),
                      ),
                      onPressed: onSkip,
                      child: const Text('Skip'),
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.only(right: 12),
                    child: Text(
                      'Step $step of $steps'.toUpperCase(),
                      style: AppText.eyebrow(fg.withValues(alpha: 0.8)).copyWith(fontWeight: FontWeight.w600),
                    ),
                  ),
                  const SizedBox(height: 8),
                  Padding(
                    padding: const EdgeInsets.only(right: 12),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Flexible(
                          child: Heading(
                            lines: page.heading,
                            color: fg,
                            size: compact ? 31.0 : 35.0,
                            brushColor: mango ? BlockInk.paper : null,
                          ),
                        ),
                        Padding(
                          padding: const EdgeInsets.only(left: 6, bottom: 10),
                          child: SparkRays(color: fg.withValues(alpha: 0.8), size: 22),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 10),
                  ConstrainedBox(
                    constraints: BoxConstraints(minHeight: avatar - overlap),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Align(
                            alignment: Alignment.topLeft,
                            child: Transform.rotate(
                              angle: -4 * math.pi / 180,
                              alignment: Alignment.centerLeft,
                              child: CustomPaint(
                                foregroundPainter: SwooshPainter(tagline.withValues(alpha: 0.85)),
                                child: Padding(
                                  padding: const EdgeInsets.only(top: 4, bottom: 12),
                                  child: Text(
                                    page.tagline,
                                    maxLines: 3,
                                    overflow: TextOverflow.ellipsis,
                                    style: scriptStyle(tagline, compact ? 18.0 : 20.0).copyWith(
                                      height: 1.12,
                                      shadows: mango
                                          ? null
                                          : [Shadow(color: shade.withValues(alpha: 0.55), blurRadius: 6)],
                                    ),
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ),
                        SizedBox(width: avatar + 4),
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

/// The paper sheet: the circle and sticker reach up out of it; then title, line, tiles and the banner.
class _Sheet extends StatelessWidget {
  const _Sheet({required this.page, required this.avatar, required this.overlap, required this.banner});

  final _Page page;
  final double avatar;
  final double overlap;

  /// The banner's height, or null on short screens.
  final double? banner;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final theme = Theme.of(context);
    final rise = avatar - overlap;
    final stickerTop = -rise + avatar * 0.16;
    final stickerRight = 12 + avatar * 0.58;
    return Stack(
      clipBehavior: Clip.none,
      children: [
        Container(
          width: double.infinity,
          decoration: BoxDecoration(
            color: c.paper,
            borderRadius: const BorderRadius.vertical(top: Radius.circular(AppRadius.sheet)),
          ),
          padding: EdgeInsets.fromLTRB(20, overlap + 18, 20, 16),
          child: EntranceMotion(
            rise: 24,
            child: Column(
              children: [
                Semantics(
                  header: true,
                  child: Text(page.title, textAlign: TextAlign.center, style: AppText.display(c.ink, 25, height: 1.05)),
                ),
                const SizedBox(height: 8),
                Text(
                  page.body,
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    fontFamily: AppFonts.body,
                    fontWeight: FontWeight.w400,
                    fontSize: 14,
                    height: 1.4,
                    color: c.muted,
                  ),
                ),
                const SizedBox(height: 18),
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    for (var i = 0; i < page.tiles.length; i++) ...[
                      if (i > 0) const SizedBox(width: 8),
                      Expanded(child: _FeatureTile(tile: page.tiles[i], seed: i)),
                    ],
                  ],
                ),
                if (banner != null) ...[
                  const SizedBox(height: 16),
                  _FoodBanner(page: page, height: banner!),
                ],
              ],
            ),
          ),
        ),
        Positioned(
          left: 22,
          top: 16,
          child: ExcludeSemantics(
            child: CustomPaint(size: const Size(44, 22), painter: GullsPainter(c.muted.withValues(alpha: 0.5))),
          ),
        ),
        Positioned(
          top: -rise,
          right: 12,
          child: EntranceMotion(child: AvatarCircle(slot: page.slot, size: avatar)),
        ),
        Positioned(
          top: stickerTop,
          right: stickerRight,
          child: ConstrainedBox(
            constraints: BoxConstraints(maxWidth: math.max(120.0, MediaQuery.sizeOf(context).width - stickerRight - 12)),
            child: Sticker(label: page.sticker, tone: page.stickerTone, tilt: -0.02),
          ),
        ),
        Positioned(
          top: stickerTop + 34,
          right: 12 + avatar - 4,
          child: SparkRays(color: page.tone.foreground(c).withValues(alpha: 0.75), size: 20, flip: true),
        ),
      ],
    );
  }
}

/// One feature tile: a picture on a pastel blob, a bold label and a two-line muted caption. Screen readers hear
/// "label, caption".
class _FeatureTile extends StatelessWidget {
  const _FeatureTile({required this.tile, required this.seed});

  final _Tile tile;
  final int seed;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final tint = switch (tile.tint) {
      _Tint.teal => c.tealSoft,
      _Tint.ice => c.iceSoft,
      _Tint.coral => c.coralSoft,
      _Tint.mango => c.mangoSoft,
    };
    return Semantics(
      key: ValueKey('tile-${tile.slot.code}'),
      container: true,
      label: '${tile.label}, ${tile.caption}',
      excludeSemantics: true,
      child: LayoutBuilder(
        builder: (context, box) {
          final blob = math.min(box.maxWidth, 84.0);
          return Column(
            children: [
              SizedBox.square(
                dimension: blob,
                child: Stack(
                  alignment: Alignment.center,
                  children: [
                    Positioned.fill(child: CustomPaint(painter: BlobPainter(tint, seed: seed))),
                    Illustration(
                      slot: tile.slot,
                      size: blob * 0.86,
                      excludeFromSemantics: true,
                      fallback: Icon(
                        tile.icon,
                        key: ValueKey('tile-fallback-${tile.slot.code}'),
                        size: blob * 0.44,
                        color: c.tealDeep,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 6),
              Text(
                tile.label,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontFamily: AppFonts.body,
                  fontFamilyFallback: FreshFonts.sansFallback,
                  fontWeight: FontWeight.w700,
                  fontSize: 14,
                  height: 1.2,
                  color: c.ink,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                tile.caption,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontFamily: AppFonts.body,
                  fontFamilyFallback: FreshFonts.sansFallback,
                  fontWeight: FontWeight.w400,
                  fontSize: 12,
                  height: 1.3,
                  color: c.muted,
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

/// The wide food banner: the picture on a soft teal wave, a handwritten line and a small arrow.
class _FoodBanner extends StatelessWidget {
  const _FoodBanner({required this.page, required this.height});

  final _Page page;
  final double height;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final (first, second) = page.bannerLines;
    final script = scriptStyle(c.ink, 17).copyWith(height: 1.1);
    return Semantics(
      key: ValueKey('banner-${page.banner.code}'),
      container: true,
      label: '$first $second',
      excludeSemantics: true,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(22),
        child: SizedBox(
          height: height,
          child: CustomPaint(
            painter: BannerWavePainter(base: c.iceSoft, wave: c.tealSoft),
            child: Row(
              children: [
                Expanded(
                  flex: 11,
                  child: LayoutBuilder(
                    builder: (context, box) => Illustration(
                      slot: page.banner,
                      size: box.maxWidth,
                      height: height,
                      excludeFromSemantics: true,
                      fallback: CustomPaint(
                        key: ValueKey('banner-fallback-${page.banner.code}'),
                        size: Size(box.maxWidth, height),
                        painter: BannerFoodPainter(
                          scene: page.scene,
                          ice: BlockInk.paper.withValues(alpha: 0.9),
                          silver: const Color(0xFF9AA8AD),
                          coral: c.coral,
                          mango: c.mango,
                          teal: c.tealText,
                          wood: const Color(0xFFD9A86C),
                        ),
                      ),
                    ),
                  ),
                ),
                Expanded(
                  flex: 8,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(4, 10, 12, 10),
                    child: FittedBox(
                      fit: BoxFit.scaleDown,
                      child: Transform.rotate(
                        angle: -6 * math.pi / 180,
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(first, style: script),
                            Text(second, style: script),
                            const SizedBox(height: 2),
                            CustomPaint(
                              size: const Size(34, 18),
                              painter: ArrowDoodlePainter(c.coralText),
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
        ),
      ),
    );
  }
}
