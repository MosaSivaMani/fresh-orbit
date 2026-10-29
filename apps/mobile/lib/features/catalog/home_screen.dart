// Home (D176, D182): the approved preview docs/design/mockups/app-home-rich.html (390 × 873, from the owner's
// reference home-reference-owner.jpg) on one grid. Every block starts and ends FreshLayout.gutter (16) from the
// screen edges: the delivery header, the greeting, "What's cooking today?" with the brush and sparks, the coral
// handwritten line, the dark-teal hero, two equal cards 12 apart, six equal category columns and the specials
// row, which starts on the gutter and lets the second card peek. The harbour, the palms and the sparks are drawn
// with the theme package's scenery and are not read by screen readers.
//
// The first screen is unchanged (D182, D221): it is laid out at the screen width and scaled down to the space
// above the cart bar and the menu (both live in the shell, so they never cover it), and a first screen shorter
// than that gives the room left to the offer pictures. The page then scrolls on (D292, owner 2026-10-29), with
// clamping physics (no bounce), into the sections below it (D293): "Popular today" (the server's featured
// products, hidden when it sends none, D186), "Fresh in three steps", "Our promise", the help card and the sea
// floor. At a system text scale of 1.5 or more the first screen keeps its natural size instead of shrinking. The chip, cards, category tiles and offer cards
// are the theme package's parts (D207); categories and offers without a server picture show the owner's bundled
// pictures (D220).
//
// Every business value comes from the server (rule 4): the pincode and city (serviceability), the first name
// (profile), the next slot, the categories and their pictures, the offers, the featured products and their
// prices, and the cart in the shell. The sections below the first screen use the onboarding's approved words;
// "Kept chilled" is said only when the serving store has cold storage (D212).

import 'dart:math' as math;

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
import '../../widgets/states.dart';
import 'product_grid.dart';

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

/// At this system text scale and above the home's first screen keeps its natural size instead of shrinking to fit.
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
              final Widget first;
              if (MediaQuery.textScalerOf(context).scale(1) >= homeScrollTextScale) {
                first = _HomePage(data: data, minHeight: 0);
              } else {
                // Full width always: a first screen taller than the space is laid out wider and scaled down,
                // never narrowed with side gaps (D182). A shorter one gives the room left to the specials, so no
                // dead gap is left above the sections below (D221).
                first = SizedBox(
                  height: box.maxHeight,
                  child: Align(
                    alignment: Alignment.topCenter,
                    child: FreshFitWidth(
                      key: const Key('homeFit'),
                      child: SizedBox(
                        width: double.infinity,
                        // Laid out wider to be scaled down, the page fills the screen height at that scale too.
                        child: LayoutBuilder(
                          builder: (context, page) => _HomePage(
                            data: data,
                            minHeight: box.maxWidth > 0
                                ? box.maxHeight * page.maxWidth / box.maxWidth
                                : box.maxHeight,
                          ),
                        ),
                      ),
                    ),
                  ),
                );
              }
              return SingleChildScrollView(
                key: const Key('homeScroll'),
                physics: const ClampingScrollPhysics(),
                child: SizedBox(
                  width: box.maxWidth,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [first, _HomeSections(products: data.featuredProducts)],
                  ),
                ),
              );
            },
          ),
        ),
      ),
    );
  }
}

class _HomePage extends ConsumerWidget {
  const _HomePage({required this.data, required this.minHeight});

  final HomeData data;

  /// The height the page fills; the specials take what the blocks above leave (0: natural height).
  final double minHeight;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final service = ref.watch(currentServiceabilityProvider).value;
    final keptCold = service?.hasColdStorage == true;
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
        // The owner's harbour picture in the top-right corner, behind the header and the greeting, faded into the
        // page (D320). The boat sits in the greeting line's own room below, so nothing covers text.
        const Positioned(top: 0, right: 0, child: HarbourScene(key: Key('homeHarbour'))),
        FreshGrowTail(
          minHeight: minHeight,
          head: Padding(
            padding: FreshLayout.page,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                _DeliveryHeader(service: service),
                // The real boat and its gulls end the greeting line, clear of the heading's sparks (D320).
                Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Expanded(
                      child: Text(
                        name == null || name.isEmpty ? hello : '$hello, $name',
                        key: const Key('homeGreeting'),
                        style: _inter(c.muted, 15, FontWeight.w500),
                      ),
                    ),
                    const Padding(
                      padding: EdgeInsets.only(left: 8, bottom: HarbourBoat.headingClearance),
                      child: HarbourBoat(key: Key('homeHarbourBoat')),
                    ),
                  ],
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
                    FreshFeatureCard(
                      key: const Key('homeCardCleaned'),
                      color: HomeColors.coralCard,
                      accent: HomeColors.coralText,
                      icon: const _KnifeIcon(),
                      title: 'Cleaned',
                      brushed: 'your way',
                      titleStyle: _cardTitle,
                      body: 'Pick your cut, we prepare it.',
                      action: 'Choose cut',
                      picture: _cardPicture(
                        IllustrationSlot.homeCardCleaned,
                        IllustrationSlot.onboardingBannerCleaned,
                        _cleanedPicture,
                      ),
                      pictureSize: const Size.square(_cleanedPicture),
                      onTap: () => context.go('/shop'),
                    ),
                    // "Kept cold" is said only when the serving store has cold storage (D212, D239); otherwise
                    // the same card offers the delivery terms without the claim.
                    FreshFeatureCard(
                      key: const Key('homeCardCold'),
                      color: HomeColors.mangoCard,
                      accent: HomeColors.tealText,
                      icon: const Snowflake(color: HomeColors.tealText, scale: 0.62),
                      // The plain words are shorter than the claim, so the card keeps its height while loading.
                      title: keptCold ? 'Kept cold' : 'Slots',
                      secondLine: keptCold ? 'till your door' : 'to your door',
                      titleStyle: _cardTitle,
                      body: keptCold ? 'Fresh on ice in every order.' : 'Fee and minimum order.',
                      action: 'See delivery',
                      // Below the title like the cleaned card's picture, so it never covers "door".
                      picture: _cardPicture(
                        IllustrationSlot.homeCardRider,
                        IllustrationSlot.onboardingTileColdBox,
                        _riderPicture,
                      ),
                      pictureSize: const Size.square(_riderPicture),
                      pictureBottom: 22,
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
          tail: data.banners.isEmpty ? const SizedBox(height: 4) : _Specials(banners: data.banners, color: c.ink),
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
              if (service.hasColdStorage == true)
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

/// Pin, "Delivering to `pincode` ⌄" with the city and its state under it (both from the server), and the search
/// and wishlist buttons on the right.
class _DeliveryHeader extends ConsumerWidget {
  const _DeliveryHeader({required this.service});

  final Serviceability? service;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final pincode = ref.watch(pincodeProvider);
    final here = service?.pincode == pincode ? service : null;
    // The state name is the city's own data (D209); it is left out when the server does not send it (D238).
    final city = switch (here) {
      Serviceability(cityName: final name?, :final cityStateName) => [
        name,
        if (cityStateName != null && cityStateName.isNotEmpty) cityStateName,
      ].join(', '),
      _ => null,
    };
    return ConstrainedBox(
      constraints: const BoxConstraints(minHeight: 52),
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
/// "Shop fresh catch" pill and the transparent picture anchored bottom-right, the teal showing through.
class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.nextSlot});

  final SlotWindow? nextSlot;

  static const double height = 194;

  /// The hero picture's width over height (1536 × 1024).
  static const double pictureAspect = 3 / 2;

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
              // 236 of the preview's 358-wide card.
              final pictureWidth = box.maxWidth * 236 / 358;
              return Stack(
                children: [
                  // Edge to edge between the gutters, whatever width the text needs.
                  SizedBox(width: box.maxWidth),
                  // The transparent picture anchored bottom-right at its natural 3:2 shape; the teal shows through.
                  Positioned(
                    right: -2,
                    bottom: 0,
                    width: pictureWidth,
                    height: pictureWidth / pictureAspect,
                    child: Illustration(
                      key: const Key('homeHeroPicture'),
                      slot: IllustrationSlot.homeHeroCatch,
                      size: pictureWidth,
                      height: pictureWidth / pictureAspect,
                      excludeFromSemantics: true,
                      fallback: Illustration(
                        slot: IllustrationSlot.onboardingFresh,
                        size: pictureWidth,
                        height: pictureWidth / pictureAspect,
                        fit: BoxFit.cover,
                        focus: const Alignment(0.2, -0.4),
                        excludeFromSemantics: true,
                      ),
                    ),
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
                              // Fitted to the width, never cut: the whole time window always shows.
                              FreshChip(
                                key: const Key('homeNextSlot'),
                                icon: Icons.schedule_rounded,
                                color: HomeColors.chip,
                                text: '$slot ›',
                              ),
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
                            const FreshPill(text: 'Shop fresh catch →', color: FreshHighlightColors.ink, size: 12.5),
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

/// The small cards' title: Baloo 800, 16.5.
final TextStyle _cardTitle = AppText.display(FreshHighlightColors.ink, 16.5, height: 1.02);

/// The small cards' pictures, square, on the lower right under the title.
const double _cleanedPicture = 84;
const double _riderPicture = 90;

/// A small card's picture: the home slot's PNG when bundled, else an onboarding stand-in, else the drawn
/// placeholder; never a broken image.
Widget _cardPicture(IllustrationSlot slot, IllustrationSlot standIn, double size) => Illustration(
  key: ValueKey('homePicture-${slot.code}'),
  slot: slot,
  size: size,
  excludeFromSemantics: true,
  fallback: Illustration(slot: standIn, size: size, excludeFromSemantics: true),
);

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

/// Six equal columns: the first five featured categories (server order) and "More". A category's picture is the
/// server's; with none (the demo fixtures) the owner's bundled picture for its slug (D220), else its first letter.
class _CategoryRow extends StatelessWidget {
  const _CategoryRow({required this.categories});

  final List<Category> categories;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final shown = categories.take(5).toList();
    return FreshColumns(
      key: const Key('homeCategories'),
      gap: 0,
      children: [
        for (var i = 0; i < 5; i++)
          if (i < shown.length)
            FreshCategoryTile(
              key: ValueKey('homeCategory-${shown[i].slug}'),
              label: shown[i].name,
              picture: _categoryPicture(shown[i]),
              tint: HomeColors.categoryTints[i % HomeColors.categoryTints.length],
              labelColor: c.ink,
              shadow: c.softShadow,
              onTap: () => context.go(Uri(path: '/shop', queryParameters: {'category': shown[i].slug}).toString()),
            )
          else
            const SizedBox.shrink(),
        FreshCategoryTile(
          key: const Key('homeCategoryMore'),
          label: 'More',
          tint: BlockInk.paper,
          icon: Icons.grid_view_rounded,
          labelColor: c.ink,
          shadow: c.softShadow,
          onTap: () => context.go('/shop'),
        ),
      ],
    );
  }
}

/// The server's picture of [category]; else the bundled one for its slug; else (or when it fails) its letter.
Widget _categoryPicture(Category category) {
  final initial = FreshCategoryTile.initial(category.name);
  final path = BundledPictures.category(category.slug);
  final bundled = path == null ? initial : BundledPicture(path: path, fallback: initial);
  final url = category.imageUrl;
  if (url == null || url.isEmpty) return bundled;
  return Image.network(url, fit: BoxFit.cover, excludeFromSemantics: true, errorBuilder: (_, _, _) => bundled);
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
            child: FittedBox(
              fit: BoxFit.scaleDown,
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

/// "Today's special" and the offers under it. Given a height (the room the blocks above leave, D221) the cards'
/// pictures grow into it, up to the 3:2 frame (D193), so no dead gap is left above the cart bar; with no height
/// (very large text, scrolling) they keep their smallest size.
class _Specials extends StatelessWidget {
  const _Specials({required this.banners, required this.color});

  final List<HomeBanner> banners;
  final Color color;

  static const double _top = 10;
  static const double _bottom = 4;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, box) {
      final header = Padding(padding: FreshLayout.page, child: _SpecialsHeader(color: color));
      if (!box.maxHeight.isFinite) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            const SizedBox(height: _top),
            header,
            const SizedBox(height: FreshLayout.cardGap),
            _SpecialsRow(banners: banners, room: null),
            const SizedBox(height: _bottom),
          ],
        );
      }
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: _top),
          header,
          const SizedBox(height: FreshLayout.cardGap),
          Expanded(
            child: LayoutBuilder(
              builder: (context, row) => Align(
                alignment: Alignment.topCenter,
                child: _SpecialsRow(banners: banners, room: math.max(0.0, row.maxHeight - _bottom)),
              ),
            ),
          ),
          const SizedBox(height: _bottom),
        ],
      );
    },
  );
}

/// The offers (server banners) in a row that starts on the gutter; 200 wide so the second card peeks.
class _SpecialsRow extends StatelessWidget {
  const _SpecialsRow({required this.banners, required this.room});

  final List<HomeBanner> banners;

  /// The height the row may take; null keeps the smallest picture.
  final double? room;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final text = FreshSpecialCard.textHeight(MediaQuery.textScalerOf(context));
    const largest = FreshSpecialCard.defaultWidth * 2 / 3;
    final picture = room == null
        ? FreshSpecialCard.minPictureHeight
        : (room! - text).clamp(FreshSpecialCard.minPictureHeight, largest).toDouble();
    return SizedBox(
      height: picture + text,
      child: ListView.separated(
        key: const Key('homeSpecials'),
        scrollDirection: Axis.horizontal,
        physics: const ClampingScrollPhysics(),
        clipBehavior: Clip.none,
        padding: FreshLayout.page,
        itemCount: banners.length,
        separatorBuilder: (_, _) => const SizedBox(width: FreshLayout.cardGap),
        itemBuilder: (context, i) {
          final banner = banners[i];
          final route = banner.route;
          final tint = HomeColors.offerTints[i % 2];
          return FreshSpecialCard(
            title: banner.title,
            subtitle: banner.subtitle,
            badge: banner.badge,
            tint: tint,
            pictureHeight: picture,
            picture: _specialPicture(banner, i, tint),
            // A category opens the Shop tab (like the category tiles); a product page is pushed so Back returns.
            onTap: route == null
                ? null
                : () {
                    if (route.startsWith('/shop')) {
                      context.go(route);
                    } else {
                      context.push(route);
                    }
                  },
            paper: c.paper,
            ink: c.ink,
            muted: c.muted,
            chevronBackground: c.sand,
            shadow: c.softShadow,
          );
        },
      ),
    );
  }
}

/// The server's offer picture; else (the demo fixtures, or when it fails) the owner's bundled offer picture
/// (D220); else the soft tint.
Widget _specialPicture(HomeBanner banner, int index, Color tint) {
  final soft = ColoredBox(color: tint);
  final bundled = BundledPicture(path: BundledPictures.special(banner.linkSlug, index), fallback: soft);
  final url = banner.imageUrl;
  if (url == null || url.isEmpty) return bundled;
  return Image.network(url, fit: BoxFit.cover, excludeFromSemantics: true, errorBuilder: (_, _, _) => bundled);
}

/// The most featured products "Popular today" shows: three rows of two on a phone.
const int homePopularCount = 6;

/// The sections below the first screen (D293): "Popular today" (the server's featured products, hidden when it
/// sends none, D186), "Fresh in three steps", "Our promise" on the dark-teal block, the help card and the sea
/// floor. The steps and promises use the onboarding's approved words and pictures.
class _HomeSections extends ConsumerWidget {
  const _HomeSections({required this.products});

  final List<ProductSummary> products;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    final keptCold = ref.watch(currentServiceabilityProvider).value?.hasColdStorage == true;
    final green = dark ? c.tealSoft : const Color(0xFFE2F1DD);
    final blue = dark ? c.iceSoft : const Color(0xFFDDF1F7);
    final peach = dark ? c.coralSoft : const Color(0xFFFBE6DC);
    final popular = products.take(homePopularCount).toList();
    return Column(
      key: const Key('homeSections'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (popular.isNotEmpty) ...[
          const SizedBox(height: FreshLayout.sectionGap),
          Padding(
            padding: FreshLayout.page,
            child: FreshSectionHeader(
              text: 'Popular',
              brushed: 'today',
              color: c.ink,
              action: 'See all →',
              actionKey: const Key('homePopularSeeAll'),
              onAction: () => context.go('/shop'),
            ),
          ),
          const SizedBox(height: FreshLayout.cardGap),
          GridView.builder(
            key: const Key('homePopular'),
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            padding: FreshLayout.page,
            gridDelegate: productGridDelegate,
            itemCount: popular.length,
            itemBuilder: (context, i) => ShopProductCard(product: popular[i], index: i),
          ),
        ],
        const SizedBox(height: FreshLayout.sectionGap),
        Padding(
          padding: FreshLayout.page,
          child: FreshSectionHeader(text: 'Fresh in', brushed: 'three steps', color: c.ink),
        ),
        const SizedBox(height: FreshLayout.cardGap),
        Padding(
          key: const Key('homeSteps'),
          padding: FreshLayout.page,
          child: FreshColumns(
            children: [
              _tile(
                1,
                IllustrationSlot.onboardingTileYourCut,
                'Pick your cut',
                'Whole, steaks, curry or fillet',
                Icons.restaurant_rounded,
                peach,
                c,
              ),
              _tile(
                2,
                IllustrationSlot.onboardingTileCleanedByHand,
                'Cleaned by hand',
                'Scaled, gutted, washed',
                Icons.water_drop_rounded,
                blue,
                c,
              ),
              _tile(
                3,
                IllustrationSlot.onboardingTileDeliveredFresh,
                'At your door',
                'In the slot you pick',
                Icons.local_shipping_rounded,
                green,
                c,
              ),
            ],
          ),
        ),
        const SizedBox(height: FreshLayout.sectionGap),
        Padding(
          padding: FreshLayout.page,
          child: DecoratedBox(
            key: const Key('homePromise'),
            decoration: BoxDecoration(
              color: HomeColors.hero,
              borderRadius: BorderRadius.circular(FreshLayout.heroRadius),
            ),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 6, 16, 18),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const FreshSectionHeader(text: 'Our', brushed: 'promise', color: BlockInk.paper),
                  const SizedBox(height: FreshLayout.cardGap),
                  FreshColumns(
                    children: [
                      _tile(
                        null,
                        IllustrationSlot.onboardingTileMorningCatch,
                        'Morning catch',
                        'Direct from local boats',
                        Icons.set_meal_rounded,
                        HomeColors.heroBody,
                        c,
                        onDark: true,
                      ),
                      _tile(
                        null,
                        IllustrationSlot.onboardingTileNetWeight,
                        'Pay net weight',
                        'Weighed after cleaning',
                        Icons.scale_rounded,
                        HomeColors.heroBody,
                        c,
                        onDark: true,
                      ),
                      // "Kept chilled" only when the serving store has cold storage (D212); else how to pay.
                      if (keptCold)
                        _tile(
                          null,
                          IllustrationSlot.onboardingTileKeptChilled,
                          'Kept chilled',
                          'On ice all the way',
                          Icons.ac_unit_rounded,
                          HomeColors.heroBody,
                          c,
                          onDark: true,
                        )
                      else
                        _tile(
                          null,
                          IllustrationSlot.onboardingTileCashUpi,
                          'Cash or UPI',
                          'Pay when it arrives',
                          Icons.payments_rounded,
                          HomeColors.heroBody,
                          c,
                          onDark: true,
                        ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
        const SizedBox(height: FreshLayout.sectionGap),
        Padding(
          padding: FreshLayout.page,
          child: FreshHelpCard(
            key: const Key('homeHelp'),
            title: 'Need a hand?',
            body: 'Questions about an order, a cut or a delivery? We are here to help.',
            action: 'Get help →',
            color: HomeColors.mangoCard,
            onTap: () => context.go('/account/help'),
          ),
        ),
        const SizedBox(height: FreshLayout.sectionGap),
        const FreshSeaFloor(key: Key('homeSeaFloor'), includeBottomInset: false),
      ],
    );
  }
}

/// A step or promise tile with the onboarding tile's picture, or its icon until the picture is bundled.
Widget _tile(
  int? step,
  IllustrationSlot slot,
  String label,
  String caption,
  IconData icon,
  Color tint,
  IllustratedColors c, {
  bool onDark = false,
}) => FreshStepTile(
  key: ValueKey('homeTile-${slot.code}'),
  step: step,
  label: label,
  caption: caption,
  tint: tint,
  ink: onDark ? BlockInk.paper : c.ink,
  muted: onDark ? HomeColors.heroBody : c.muted,
  picture: Illustration(
    slot: slot,
    size: FreshStepTile.pictureSize,
    height: FreshStepTile.pictureHeight,
    excludeFromSemantics: true,
    fallback: Center(
      child: Padding(
        padding: const EdgeInsets.only(top: 14),
        child: Icon(icon, key: ValueKey('homeTile-fallback-${slot.code}'), size: 32, color: c.tealDeep),
      ),
    ),
  ),
);
