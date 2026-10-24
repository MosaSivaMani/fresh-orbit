// Home (D176, D182): the approved preview docs/design/mockups/app-home-rich.html (390 × 873, from the owner's
// reference home-reference-owner.jpg) on one grid. Every block starts and ends FreshLayout.gutter (16) from the
// screen edges: the delivery header, the greeting, "What's cooking today?" with the brush and sparks, the coral
// handwritten line, the dark-teal hero, two equal cards 12 apart, six equal category columns and the specials
// row, which starts on the gutter and lets the second card peek. The harbour, the palms and the sparks are drawn
// with the theme package's scenery and are not read by screen readers.
//
// The page fits one screen (D182): it is laid out at the screen width and scaled down to the space above the
// cart bar and the menu (both live in the shell, so they never cover it). Only at a system text scale of 1.5 or
// more does it scroll, with clamping physics (no bounce).
//
// Every business value comes from the server (rule 4): the pincode and city (serviceability), the first name
// (profile), the next slot, the categories and their pictures, the offers, and the cart in the shell.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/catalog.dart';
import '../../api/models/locations.dart';
import '../../core/format.dart';
import '../../illustrations/illustration.dart';
import '../../state/account_state.dart';
import '../../state/catalog_state.dart';
import '../../state/location_state.dart';
import '../../state/session.dart';
import '../../theme/app_theme.dart';
import '../../widgets/states.dart';

/// Pincode chip for app bars: "Delivering to 600041" → change pincode.
class PincodeChip extends ConsumerWidget {
  const PincodeChip({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pincode = ref.watch(pincodeProvider);
    return Semantics(
      button: true,
      label: pincode == null ? 'Set delivery pincode' : 'Delivering to $pincode, change',
      excludeSemantics: true,
      child: InkWell(
        borderRadius: BorderRadius.circular(FreshRadius.full),
        onTap: () => context.push('/location?change=1'),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minHeight: 48),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.location_on_outlined, size: 20),
              const SizedBox(width: FreshSpacing.s1),
              Flexible(
                child: Text(
                  pincode == null ? 'Set pincode' : 'Delivering to $pincode',
                  style: Theme.of(context).textTheme.titleSmall,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const Icon(Icons.expand_more, size: 20),
            ],
          ),
        ),
      ),
    );
  }
}

/// "Good morning" from the device clock: wording only, not a business value.
String greeting(DateTime now) => now.hour < 12
    ? 'Good morning'
    : now.hour < 17
    ? 'Good afternoon'
    : 'Good evening';

/// Colours of the home preview that stay the same in light and dark (blocks with ink or paper text on them).
abstract final class HomeColors {
  static const Color hero = Color(0xFF075E54);
  static const Color heroBody = Color(0xFFD7EEEA);
  static const Color chip = Color(0xFFF5D04A);
  static const Color coralCard = Color(0xFFF59B7C);
  static const Color mangoCard = Color(0xFFF7C66B);
  static const Color coralText = Color(0xFFC65A3F);
  static const Color tealText = Color(0xFF0B7D70);
  static const List<Color> categoryTints = [
    Color(0xFFDDF0EC),
    Color(0xFFFBE0D6),
    Color(0xFFFCEBC8),
    Color(0xFFF6DDE6),
    Color(0xFFE2EFD9),
  ];
  static const List<Color> offerTints = [Color(0xFFE7F2EE), Color(0xFFFBE6DC)];
}

/// At this system text scale and above the home scrolls instead of shrinking to fit.
const double homeScrollTextScale = 1.5;

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(homeProvider);
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: AsyncValueView<HomeData>(
          value: home,
          onRetry: () {
            ref.invalidate(currentServiceabilityProvider);
            ref.invalidate(homeProvider);
          },
          loading: const Center(child: CircularProgressIndicator()),
          data: (data) => LayoutBuilder(
            builder: (context, box) {
              final page = SizedBox(width: box.maxWidth, child: _HomePage(data: data));
              if (MediaQuery.textScalerOf(context).scale(1) >= homeScrollTextScale) {
                return SingleChildScrollView(key: const Key('homeScroll'), physics: const ClampingScrollPhysics(), child: page);
              }
              return FittedBox(
                key: const Key('homeFit'),
                fit: BoxFit.scaleDown,
                alignment: Alignment.topCenter,
                child: page,
              );
            },
          ),
        ),
      ),
    );
  }
}

class _HomePage extends ConsumerWidget {
  const _HomePage({required this.data});

  final HomeData data;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final service = ref.watch(currentServiceabilityProvider).value;
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    final name = signedIn ? ref.watch(profileProvider).value?.fullName?.trim().split(' ').first : null;
    final hello = greeting(DateTime.now());
    return Stack(
      clipBehavior: Clip.none,
      children: [
        // Soft palms peeking in at the edges (preview y 300 and 680 on the left, 560 on the right).
        const Positioned.fill(
          child: ExcludeSemantics(
            child: CustomPaint(
              painter: PalmsPainter([
                ScenePalm(at: Offset(-6, 256), scale: 0.9, color: FreshScenery.seaDeep, mirrored: true),
                ScenePalm(at: Offset(-4, 636), scale: 0.75, mirrored: true),
                ScenePalm(at: Offset(-4, 516), scale: 0.6, fromRight: true),
              ]),
            ),
          ),
        ),
        // The harbour in the top-right corner (preview x 168–390, y 56–206).
        const Positioned(top: 12, right: 0, child: HarbourScene(key: Key('homeHarbour'))),
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: FreshLayout.page,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  _DeliveryHeader(service: service),
                  Text(
                    name == null || name.isEmpty ? hello : '$hello, $name',
                    key: const Key('homeGreeting'),
                    style: _inter(c.muted, 15, FontWeight.w500),
                  ),
                  const SizedBox(height: 2),
                  _Heading(color: c.ink),
                  const SizedBox(height: 4),
                  const Padding(
                    padding: EdgeInsets.only(left: 1),
                    child: ScriptLine(
                      text: 'From our coast to your kitchen',
                      fontFamily: AppFonts.script,
                      fontFamilyFallback: FreshFonts.sansFallback,
                      color: FreshHighlightColors.coralDeep,
                      fontSize: 19,
                      swooshSize: Size(140, 9),
                      swooshWidth: 2.2,
                    ),
                  ),
                  const SizedBox(height: 10),
                  if (data.locationRequired) ...[
                    const InfoBanner(
                      text: 'Set your pincode to see prices and availability.',
                      tone: BannerTone.warning,
                    ),
                    const SizedBox(height: 10),
                  ],
                  _HeroCard(nextSlot: service?.nextSlot),
                  const SizedBox(height: FreshLayout.cardGap),
                  FreshColumns(
                    stretch: true,
                    children: [
                      _SmallCard(
                        key: const Key('homeCardCleaned'),
                        color: HomeColors.coralCard,
                        accent: HomeColors.coralText,
                        icon: const _KnifeIcon(),
                        title: 'Cleaned',
                        brushed: 'your way',
                        body: 'Pick your cut, we prepare it.',
                        action: 'Choose cut',
                        picture: const _CardPicture.cleaned(),
                        onTap: () => context.go('/shop'),
                      ),
                      _SmallCard(
                        key: const Key('homeCardCold'),
                        color: HomeColors.mangoCard,
                        accent: HomeColors.tealText,
                        icon: const Snowflake(color: HomeColors.tealText, scale: 0.62),
                        title: 'Kept cold',
                        secondLine: 'till your door',
                        body: 'Fresh on ice in every order.',
                        action: 'See delivery',
                        picture: const _CardPicture.rider(),
                        onTap: () => _showDelivery(context, service),
                      ),
                    ],
                  ),
                  if (data.featuredCategories.isNotEmpty) ...[
                    const SizedBox(height: FreshLayout.cardGap),
                    _CategoryRow(categories: data.featuredCategories),
                  ],
                ],
              ),
            ),
            if (data.banners.isNotEmpty) ...[
              const SizedBox(height: 10),
              Padding(padding: FreshLayout.page, child: _SpecialsHeader(color: c.ink)),
              const SizedBox(height: FreshLayout.cardGap),
              _SpecialsRow(banners: data.banners),
            ],
            const SizedBox(height: 4),
          ],
        ),
      ],
    );
  }
}

TextStyle _inter(Color color, double size, FontWeight weight, {double? height, double? spacing}) => TextStyle(
  fontFamily: AppFonts.body,
  fontFamilyFallback: FreshFonts.sansFallback,
  fontWeight: weight,
  fontSize: size,
  height: height,
  letterSpacing: spacing,
  color: color,
);

/// "Kept cold → See delivery": the server's delivery terms for the pincode in a sheet, or the shop when there is
/// no serviceable pincode yet.
void _showDelivery(BuildContext context, Serviceability? service) {
  if (service == null || !service.serviceable) {
    context.go('/shop');
    return;
  }
  final lines = [
    if (service.serviceAreaName != null) service.serviceAreaName!,
    if (service.nextSlot != null)
      'Next slot ${formatTimeRange(service.nextSlot!.startAt, service.nextSlot!.endAt)}',
    if (service.deliveryFeePaise != null) 'Delivery fee ${formatRupees(service.deliveryFeePaise!)}',
    if (service.freeDeliveryAbovePaise != null)
      'Free delivery above ${formatRupees(service.freeDeliveryAbovePaise!)}',
    if (service.minOrderPaise != null) 'Minimum order ${formatRupees(service.minOrderPaise!)}',
  ];
  showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    builder: (context) {
      final c = illustratedColors(context);
      return SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, FreshLayout.sectionGap),
          child: Column(
            key: const Key('homeDeliverySheet'),
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Semantics(
                header: true,
                child: Text('Delivery to ${service.pincode}', style: AppText.display(c.ink, 22)),
              ),
              const SizedBox(height: 4),
              Text('Packed on ice in a cold box.', style: _inter(c.muted, 14, FontWeight.w400)),
              const SizedBox(height: 12),
              for (final line in lines)
                Padding(
                  padding: const EdgeInsets.only(bottom: 6),
                  child: Text(line, style: _inter(c.ink, 15, FontWeight.w600)),
                ),
            ],
          ),
        ),
      );
    },
  );
}

/// Pin, "Delivering to <pincode> ⌄" with the city under it (both from the server), and the search and wishlist
/// buttons on the right.
class _DeliveryHeader extends ConsumerWidget {
  const _DeliveryHeader({required this.service});

  final Serviceability? service;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final pincode = ref.watch(pincodeProvider);
    final city = service?.pincode == pincode ? service?.cityName : null;
    return SizedBox(
      height: 52,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Semantics(
              button: true,
              label: pincode == null
                  ? 'Set delivery pincode'
                  : ['Delivering to $pincode', ?city, 'change'].join(', '),
              excludeSemantics: true,
              child: InkWell(
                key: const Key('homeDeliveryTo'),
                borderRadius: BorderRadius.circular(12),
                onTap: () => context.push('/location?change=1'),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Padding(
                      padding: const EdgeInsets.only(top: 1),
                      child: Icon(Icons.location_on_outlined, size: 24, color: c.ink),
                    ),
                    const SizedBox(width: 8),
                    Flexible(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Flexible(
                                child: Text(
                                  pincode == null ? 'Set your pincode' : 'Delivering to $pincode',
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: _inter(c.ink, 17, FontWeight.w700, height: 1.2),
                                ),
                              ),
                              const SizedBox(width: 4),
                              Icon(Icons.expand_more_rounded, size: 18, color: c.ink),
                            ],
                          ),
                          if (city != null)
                            Text(
                              city,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: _inter(c.muted, 12.5, FontWeight.w500, height: 1.3),
                            ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(width: 8),
          _CircleButton(icon: Icons.search_rounded, label: 'Search', onTap: () => context.push('/search')),
          const SizedBox(width: 8),
          _CircleButton(
            icon: Icons.favorite_border_rounded,
            label: 'Saved',
            onTap: () => context.push('/wishlist'),
          ),
        ],
      ),
    );
  }
}

/// A 46 dp paper circle with a soft shadow and a line icon.
class _CircleButton extends StatelessWidget {
  const _CircleButton({required this.icon, required this.label, required this.onTap});

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: Tooltip(
        message: label,
        child: Material(
          color: c.paper,
          shape: const CircleBorder(),
          elevation: 2,
          shadowColor: c.softShadow,
          child: InkWell(
            customBorder: const CircleBorder(),
            onTap: onTap,
            child: SizedBox.square(
              dimension: FreshLayout.iconButton,
              child: Icon(icon, size: 21, color: c.ink),
            ),
          ),
        ),
      ),
    );
  }
}

/// "What's cooking today?" in Baloo 800, "today?" on the brush with coral sparks. One header for screen readers.
class _Heading extends StatelessWidget {
  const _Heading({required this.color});

  final Color color;

  @override
  Widget build(BuildContext context) {
    final style = AppText.display(color, 32, height: 1.1).copyWith(letterSpacing: -0.012 * 32);
    return Semantics(
      header: true,
      label: 'What’s cooking today?',
      excludeSemantics: true,
      child: FittedBox(
        fit: BoxFit.scaleDown,
        alignment: Alignment.centerLeft,
        child: Padding(
          padding: const EdgeInsets.only(top: 6, right: 40),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('What’s cooking ', style: style),
              Stack(
                clipBehavior: Clip.none,
                children: [
                  BrushHighlight(
                    degrees: -2,
                    padding: const EdgeInsets.symmetric(horizontal: 5),
                    child: Text('today?', style: style.copyWith(color: FreshHighlightColors.ink)),
                  ),
                  const Positioned(right: -36, top: -16, child: Sparks(color: FreshHighlightColors.coral)),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The dark-teal hero: next-slot chip (server), "Today's catch / is in!" with a yellow spark, a line, the
/// "Shop fresh catch" pill and the picture on the right behind a wavy teal edge.
class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.nextSlot});

  final SlotWindow? nextSlot;

  static const double height = 194;
  static const double pictureWidth = 196;

  @override
  Widget build(BuildContext context) {
    final slot = nextSlot == null ? null : 'Next slot ${formatTimeRange(nextSlot!.startAt, nextSlot!.endAt)}';
    final title = AppText.display(BlockInk.paper, 29, height: 1);
    final label = [
      'Today’s catch is in!',
      ?slot,
      'Fish, prawns, crabs and more from this morning’s boats.',
      'Shop fresh catch',
    ].join(' ');
    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: Material(
        key: const Key('homeHero'),
        color: HomeColors.hero,
        borderRadius: BorderRadius.circular(FreshLayout.heroRadius),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => context.go('/shop'),
          child: LayoutBuilder(
            builder: (context, box) {
              final textWidth = box.maxWidth * 0.52 < 186 ? box.maxWidth * 0.52 : 186.0;
              return Stack(
                children: [
                  const Positioned(
                    top: 0,
                    right: 0,
                    bottom: 0,
                    width: pictureWidth,
                    child: Illustration(
                      key: Key('homeHeroPicture'),
                      slot: IllustrationSlot.homeHeroCatch,
                      size: pictureWidth,
                      height: height,
                      fit: BoxFit.cover,
                      focus: Alignment(0.2, -0.4),
                      excludeFromSemantics: true,
                      fallback: Illustration(
                        slot: IllustrationSlot.onboardingFresh,
                        size: pictureWidth,
                        height: height,
                        fit: BoxFit.cover,
                        focus: Alignment(0.2, -0.4),
                        excludeFromSemantics: true,
                      ),
                    ),
                  ),
                  // The wavy teal edge into the picture (preview x 150–210 of the 358 card).
                  const Positioned(
                    top: 0,
                    bottom: 0,
                    right: pictureWidth - 48,
                    width: 60,
                    child: ExcludeSemantics(child: CustomPaint(painter: _WavyEdgePainter(HomeColors.hero))),
                  ),
                  ConstrainedBox(
                    constraints: const BoxConstraints(minHeight: height),
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(16, 14, 0, 14),
                      child: SizedBox(
                        width: textWidth,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            if (slot != null) ...[
                              _Chip(key: const Key('homeNextSlot'), text: '$slot ›'),
                              const SizedBox(height: 12),
                            ],
                            Text('Today’s catch', style: title),
                            Row(
                              mainAxisSize: MainAxisSize.min,
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Flexible(child: Text('is in!', style: title)),
                                const SizedBox(width: 4),
                                const Sparks(color: FreshHighlightColors.brush, scale: 0.82),
                              ],
                            ),
                            const SizedBox(height: 6),
                            SizedBox(
                              width: 176,
                              child: Text(
                                'Fish, prawns, crabs and more from this morning’s boats.',
                                style: _inter(HomeColors.heroBody, 12.5, FontWeight.w400, height: 1.35),
                              ),
                            ),
                            const SizedBox(height: 6),
                            const _Pill(text: 'Shop fresh catch →', color: FreshHighlightColors.ink, size: 12.5),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

/// The preview's `.edge`: a teal shape whose right side waves, laid over the picture's left edge (60 × 194,
/// stretched to the card's height).
class _WavyEdgePainter extends CustomPainter {
  const _WavyEdgePainter(this.color);

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    canvas
      ..save()
      ..scale(size.width / 60, size.height / 194)
      ..drawPath(
        Path()
          ..moveTo(0, 0)
          ..lineTo(28, 0)
          ..quadraticBezierTo(48, 30, 32, 62)
          ..quadraticBezierTo(14, 98, 38, 134)
          ..quadraticBezierTo(54, 164, 36, 194)
          ..lineTo(0, 194)
          ..close(),
        Paint()..color = color,
      )
      ..restore();
  }

  @override
  bool shouldRepaint(_WavyEdgePainter old) => old.color != color;
}

/// The yellow next-slot chip with a clock.
class _Chip extends StatelessWidget {
  const _Chip({super.key, required this.text});

  final String text;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
    decoration: BoxDecoration(
      color: HomeColors.chip,
      borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
    ),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.schedule_rounded, size: 13, color: FreshHighlightColors.ink),
        const SizedBox(width: 5),
        Flexible(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: _inter(FreshHighlightColors.ink, 11.5, FontWeight.w700),
          ),
        ),
      ],
    ),
  );
}

/// A paper pill with a soft shadow ("Shop fresh catch →").
class _Pill extends StatelessWidget {
  const _Pill({required this.text, required this.color, required this.size, this.padding});

  final String text;
  final Color color;
  final double size;
  final EdgeInsets? padding;

  @override
  Widget build(BuildContext context) => Container(
    padding: padding ?? const EdgeInsets.symmetric(horizontal: 13, vertical: 7),
    decoration: BoxDecoration(
      color: BlockInk.paper,
      borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
      boxShadow: const [BoxShadow(color: Color(0x1F000000), blurRadius: 6, offset: Offset(0, 2))],
    ),
    child: Text(text, maxLines: 1, overflow: TextOverflow.ellipsis, style: _inter(color, size, FontWeight.w700)),
  );
}

/// One of the two equal cards: an icon and a Baloo title (the second line on the brush or plain), a short line,
/// a pill with coloured text at the bottom and a picture popping out on the right.
class _SmallCard extends StatelessWidget {
  const _SmallCard({
    super.key,
    required this.color,
    required this.accent,
    required this.icon,
    required this.title,
    required this.body,
    required this.action,
    required this.picture,
    required this.onTap,
    this.brushed,
    this.secondLine,
  });

  final Color color;
  final Color accent;
  final Widget icon;
  final String title;

  /// The second title line, on the yellow brush.
  final String? brushed;

  /// The second title line, plain.
  final String? secondLine;
  final String body;
  final String action;
  final _CardPicture picture;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final style = AppText.display(FreshHighlightColors.ink, 16.5, height: 1.02);
    final second = brushed ?? secondLine;
    return Semantics(
      button: true,
      label: '$title ${second ?? ''}. $body $action'.replaceAll('  ', ' '),
      excludeSemantics: true,
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Positioned.fill(
            child: Material(
              color: color,
              borderRadius: BorderRadius.circular(FreshLayout.cardRadius),
              child: InkWell(borderRadius: BorderRadius.circular(FreshLayout.cardRadius), onTap: onTap),
            ),
          ),
          picture,
          if (brushed != null)
            const Positioned(
              right: 58,
              top: 6,
              child: Sparks(color: FreshHighlightColors.brush, degrees: -10, scale: 0.6),
            ),
          IgnorePointer(
            child: ConstrainedBox(
              constraints: const BoxConstraints(minHeight: 122),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Padding(padding: const EdgeInsets.only(top: 1), child: icon),
                        const SizedBox(width: 6),
                        Flexible(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(title, style: style),
                              if (brushed != null)
                                BrushHighlight(
                                  degrees: -2,
                                  padding: const EdgeInsets.symmetric(horizontal: 3),
                                  child: Text(brushed!, style: style),
                                )
                              else if (secondLine != null)
                                Text(secondLine!, style: style),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    SizedBox(
                      width: 88,
                      child: Text(body, style: _inter(FreshHighlightColors.ink, 11.5, FontWeight.w400, height: 1.3)),
                    ),
                    const Spacer(),
                    const SizedBox(height: 8),
                    _Pill(
                      text: '$action →',
                      color: accent,
                      size: 11.5,
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
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

/// The picture popping out of a small card: the home slot's PNG when bundled, else an onboarding stand-in, else
/// the drawn placeholder; never a broken image.
class _CardPicture extends StatelessWidget {
  const _CardPicture.cleaned()
    : slot = IllustrationSlot.homeCardCleaned,
      standIn = IllustrationSlot.onboardingBannerCleaned,
      width = 96,
      height = 70,
      top = null,
      bottom = 22;

  const _CardPicture.rider()
    : slot = IllustrationSlot.homeCardRider,
      standIn = IllustrationSlot.onboardingTileColdBox,
      width = 86,
      height = 96,
      top = -14,
      bottom = null;

  final IllustrationSlot slot;
  final IllustrationSlot standIn;
  final double width;
  final double height;
  final double? top;
  final double? bottom;

  @override
  Widget build(BuildContext context) => Positioned(
    right: -4,
    top: top,
    bottom: bottom,
    child: IgnorePointer(
      child: Illustration(
        key: ValueKey('homePicture-${slot.code}'),
        slot: slot,
        size: width,
        height: height,
        excludeFromSemantics: true,
        fallback: Illustration(slot: standIn, size: width, height: height, excludeFromSemantics: true),
      ),
    ),
  );
}

/// The knife icon of the "Cleaned your way" card (the preview's drawn blade).
class _KnifeIcon extends StatelessWidget {
  const _KnifeIcon();

  @override
  Widget build(BuildContext context) =>
      const ExcludeSemantics(child: CustomPaint(size: Size.square(18), painter: _KnifePainter()));
}

class _KnifePainter extends CustomPainter {
  const _KnifePainter();

  @override
  void paint(Canvas canvas, Size size) {
    final k = size.width / 20;
    canvas
      ..save()
      ..scale(k)
      ..drawPath(
        Path()
          ..moveTo(3, 17)
          ..lineTo(14, 4)
          ..arcToPoint(const Offset(17, 7), radius: const Radius.circular(2.5))
          ..lineTo(6, 18)
          ..close(),
        Paint()
          ..color = FreshHighlightColors.ink
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2
          ..strokeJoin = StrokeJoin.round,
      )
      ..restore();
  }

  @override
  bool shouldRepaint(_KnifePainter old) => false;
}

/// Six equal columns: the first five featured categories (server order) and "More".
class _CategoryRow extends StatelessWidget {
  const _CategoryRow({required this.categories});

  final List<Category> categories;

  @override
  Widget build(BuildContext context) {
    final shown = categories.take(5).toList();
    return FreshColumns(
      key: const Key('homeCategories'),
      gap: 0,
      children: [
        for (var i = 0; i < 5; i++)
          if (i < shown.length)
            _CategoryCircle(
              key: ValueKey('homeCategory-${shown[i].slug}'),
              label: shown[i].name,
              imageUrl: shown[i].imageUrl,
              tint: HomeColors.categoryTints[i % HomeColors.categoryTints.length],
              onTap: () =>
                  context.go(Uri(path: '/shop', queryParameters: {'category': shown[i].slug}).toString()),
            )
          else
            const SizedBox.shrink(),
        _CategoryCircle(
          key: const Key('homeCategoryMore'),
          label: 'More',
          tint: BlockInk.paper,
          icon: Icons.grid_view_rounded,
          onTap: () => context.go('/shop'),
        ),
      ],
    );
  }
}

class _CategoryCircle extends StatelessWidget {
  const _CategoryCircle({
    super.key,
    required this.label,
    required this.tint,
    required this.onTap,
    this.imageUrl,
    this.icon,
  });

  final String label;
  final Color tint;
  final VoidCallback onTap;
  final String? imageUrl;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final initial = Center(
      child: icon != null
          ? Icon(icon, size: 22, color: FreshHighlightColors.ink)
          : Text(
              label.isEmpty ? '' : label.characters.first.toUpperCase(),
              style: AppText.display(FreshHighlightColors.ink, 19, height: 1),
            ),
    );
    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 50,
              height: 50,
              decoration: BoxDecoration(
                color: tint,
                shape: BoxShape.circle,
                border: Border.all(color: BlockInk.paper, width: 2),
                boxShadow: [BoxShadow(color: c.softShadow, blurRadius: 6, offset: const Offset(0, 2))],
              ),
              clipBehavior: Clip.antiAlias,
              child: imageUrl == null
                  ? initial
                  : Image.network(
                      imageUrl!,
                      fit: BoxFit.cover,
                      excludeFromSemantics: true,
                      errorBuilder: (_, _, _) => initial,
                    ),
            ),
            const SizedBox(height: 5),
            FittedBox(
              fit: BoxFit.scaleDown,
              child: Text(label, maxLines: 1, style: _inter(c.ink, 12.5, FontWeight.w600)),
            ),
          ],
        ),
      ),
    );
  }
}

/// "Today's special" with the brush on "special" and a coral spark, and "See all →".
class _SpecialsHeader extends StatelessWidget {
  const _SpecialsHeader({required this.color});

  final Color color;

  @override
  Widget build(BuildContext context) {
    final style = AppText.display(color, 20, height: 1);
    return Row(
      children: [
        Expanded(
          child: Semantics(
            header: true,
            label: 'Today’s special',
            excludeSemantics: true,
            child: Align(
              alignment: Alignment.centerLeft,
              child: Stack(
                clipBehavior: Clip.none,
                children: [
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text('Today’s ', style: style),
                      BrushHighlight(
                        degrees: -2,
                        padding: const EdgeInsets.fromLTRB(3, 2, 3, 2),
                        child: Text('special', style: style.copyWith(color: FreshHighlightColors.ink)),
                      ),
                    ],
                  ),
                  const Positioned(
                    right: -30,
                    top: -12,
                    child: Sparks(color: FreshHighlightColors.coral, scale: 0.68),
                  ),
                ],
              ),
            ),
          ),
        ),
        Semantics(
          button: true,
          label: 'See all',
          excludeSemantics: true,
          child: InkWell(
            key: const Key('homeSeeAll'),
            borderRadius: BorderRadius.circular(8),
            onTap: () => context.go('/shop'),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Text('See all →', style: _inter(HomeColors.tealText, 13, FontWeight.w600)),
            ),
          ),
        ),
      ],
    );
  }
}

/// The offers (server banners) in a row that starts on the gutter; 200 wide so the second card peeks.
class _SpecialsRow extends StatelessWidget {
  const _SpecialsRow({required this.banners});

  final List<HomeBanner> banners;

  static const double width = 200;
  static const double imageHeight = 54;

  @override
  Widget build(BuildContext context) {
    final ts = MediaQuery.textScalerOf(context);
    final height = imageHeight + 4 + ts.scale(13) * 1.25 + ts.scale(11) * 1.3 + 8;
    return SizedBox(
      height: height,
      child: ListView.separated(
        key: const Key('homeSpecials'),
        scrollDirection: Axis.horizontal,
        physics: const ClampingScrollPhysics(),
        clipBehavior: Clip.none,
        padding: FreshLayout.page,
        itemCount: banners.length,
        separatorBuilder: (_, _) => const SizedBox(width: FreshLayout.cardGap),
        itemBuilder: (context, i) => _OfferCard(banner: banners[i], tint: HomeColors.offerTints[i % 2]),
      ),
    );
  }
}

class _OfferCard extends StatelessWidget {
  const _OfferCard({required this.banner, required this.tint});

  final HomeBanner banner;
  final Color tint;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final route = banner.route;
    final soft = ColoredBox(color: tint);
    return Semantics(
      button: route != null,
      label: [banner.title, ?banner.subtitle, ?banner.badge].join(', '),
      excludeSemantics: true,
      child: Container(
        width: _SpecialsRow.width,
        decoration: BoxDecoration(
          color: c.paper,
          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
          boxShadow: [BoxShadow(color: c.softShadow, blurRadius: 10, offset: const Offset(0, 2))],
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: route == null ? null : () => context.push(route),
          child: Stack(
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SizedBox(
                    height: _SpecialsRow.imageHeight,
                    child: banner.imageUrl == null
                        ? soft
                        : Image.network(
                            banner.imageUrl!,
                            fit: BoxFit.cover,
                            excludeFromSemantics: true,
                            errorBuilder: (_, _, _) => soft,
                          ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(10, 4, 34, 0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          banner.title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: _inter(c.ink, 13, FontWeight.w700, height: 1.25),
                        ),
                        if (banner.subtitle != null)
                          Text(
                            banner.subtitle!,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: _inter(c.muted, 11, FontWeight.w400, height: 1.3),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
              if (banner.badge != null)
                Positioned(
                  left: 8,
                  top: 7,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8),
                    decoration: BoxDecoration(
                      color: HomeColors.chip,
                      borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
                      border: Border.all(color: FreshHighlightColors.ink, width: 1.8),
                    ),
                    child: Text(
                      banner.badge!,
                      style: _inter(FreshHighlightColors.ink, 10.5, FontWeight.w800, height: 1.6),
                    ),
                  ),
                ),
              if (route != null)
                Positioned(
                  right: 8,
                  bottom: 9,
                  child: Container(
                    width: 22,
                    height: 22,
                    decoration: BoxDecoration(color: c.sand, shape: BoxShape.circle),
                    alignment: Alignment.center,
                    child: Icon(Icons.chevron_right_rounded, size: 16, color: c.ink),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
