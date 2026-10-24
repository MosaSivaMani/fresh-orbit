// Home (D88): pincode, a greeting, the bento grid with characters peeking from the tile corners, category
// bubbles, offers and the popular products with ADD buttons.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/catalog.dart';
import '../../core/format.dart';
import '../../state/account_state.dart';
import '../../state/catalog_state.dart';
import '../../state/location_state.dart';
import '../../state/session.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/heading.dart';
import '../../widgets/illustrated.dart';
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

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(homeProvider);
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(currentServiceabilityProvider);
            return ref.refresh(homeProvider.future);
          },
          child: AsyncValueView<HomeData>(
            value: home,
            onRetry: () => ref.invalidate(homeProvider),
            loading: const GridSkeleton(),
            data: (data) => _HomeBody(data: data),
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

class _HomeBody extends ConsumerWidget {
  const _HomeBody({required this.data});

  final HomeData data;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final service = ref.watch(currentServiceabilityProvider).value;
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    final name = signedIn ? ref.watch(profileProvider).value?.fullName?.trim().split(' ').first : null;
    final nextSlot = service?.nextSlot;
    final catchLine = data.featuredProducts.isEmpty
        ? 'Fish, prawns and crabs'
        : data.featuredProducts.take(3).map((p) => p.name).join(', ');
    final tints = softTints(c);
    return CustomScrollView(
      slivers: [
        SliverPadding(
          padding: const EdgeInsets.fromLTRB(14, 4, 6, 0),
          sliver: SliverToBoxAdapter(
            child: Row(
              children: [
                const Expanded(child: PincodeChip()),
                RoundIconButton(
                  icon: Icons.search_rounded,
                  tooltip: 'Search',
                  onPressed: () => context.push('/search'),
                ),
                RoundIconButton(
                  icon: Icons.favorite_border_rounded,
                  tooltip: 'Saved',
                  onPressed: () => context.push('/wishlist'),
                ),
              ],
            ),
          ),
        ),
        SliverPadding(
          padding: const EdgeInsets.fromLTRB(18, 2, 18, 0),
          sliver: SliverToBoxAdapter(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name == null || name.isEmpty ? greeting(DateTime.now()) : '${greeting(DateTime.now())}, $name',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: c.muted),
                ),
                const ScriptTitle(text: 'What’s cooking', script: 'today?', size: 27),
              ],
            ),
          ),
        ),
        SliverPadding(
          padding: const EdgeInsets.fromLTRB(14, 10, 14, 0),
          sliver: SliverToBoxAdapter(
            child: Column(
              children: [
                if (data.locationRequired)
                  const Padding(
                    padding: EdgeInsets.only(bottom: 10),
                    child: InfoBanner(
                      text: 'Set your pincode to see prices and availability.',
                      tone: BannerTone.warning,
                    ),
                  ),
                _BentoTile(
                  key: const Key('bentoCatch'),
                  wide: true,
                  color: c.tealText,
                  foreground: c.onTeal,
                  chip: nextSlot == null ? null : 'Next slot ${formatTimeRange(nextSlot.startAt, nextSlot.endAt)}',
                  title: 'Today’s catch is in',
                  subtitle: catchLine,
                  slot: IllustrationSlot.onboardingFresh,
                  onTap: () => context.go('/shop'),
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: _BentoTile(
                        color: c.coral,
                        foreground: BlockInk.ink,
                        title: 'Cleaned your way',
                        subtitle: 'Pick your cut',
                        slot: IllustrationSlot.onboardingCleaned,
                        onTap: () => context.go('/shop'),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: _BentoTile(
                        color: c.mango,
                        foreground: BlockInk.ink,
                        title: 'Kept cold',
                        subtitle: 'Pick a delivery slot',
                        slot: IllustrationSlot.onboardingDelivery,
                        onTap: () => context.go('/shop'),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        if (data.featuredCategories.isNotEmpty)
          SliverToBoxAdapter(
            child: SizedBox(
              height: 92,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.fromLTRB(14, 12, 14, 0),
                itemCount: data.featuredCategories.length,
                separatorBuilder: (_, _) => const SizedBox(width: 8),
                itemBuilder: (context, i) {
                  final cat = data.featuredCategories[i];
                  return CategoryBubble(
                    label: cat.name,
                    imageUrl: cat.imageUrl,
                    tint: tints[i % tints.length],
                    selected: false,
                    onTap: () => context.go(Uri(path: '/shop', queryParameters: {'category': cat.slug}).toString()),
                  );
                },
              ),
            ),
          ),
        if (data.banners.isNotEmpty)
          SliverToBoxAdapter(
            child: SizedBox(
              height: 130,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.fromLTRB(14, 12, 14, 0),
                itemCount: data.banners.length,
                separatorBuilder: (_, _) => const SizedBox(width: FreshSpacing.s3),
                itemBuilder: (context, i) => _OfferCard(banner: data.banners[i]),
              ),
            ),
          ),
        SliverPadding(
          padding: const EdgeInsets.fromLTRB(18, 18, 18, 0),
          sliver: SliverToBoxAdapter(
            child: Semantics(header: true, child: Text('Popular today', style: AppText.display(c.ink, 20))),
          ),
        ),
        if (data.featuredProducts.isEmpty)
          const SliverToBoxAdapter(
            child: EmptyState(
              slot: IllustrationSlot.outOfStock,
              title: 'Nothing featured right now',
              message: 'Browse the shop to see everything available for your area.',
            ),
          )
        else
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(12, 10, 12, 0),
            sliver: SliverGrid.builder(
              gridDelegate: productGridDelegate,
              itemCount: data.featuredProducts.length,
              itemBuilder: (context, i) => ShopProductCard(product: data.featuredProducts[i], index: i),
            ),
          ),
        const SliverToBoxAdapter(child: SizedBox(height: FreshSpacing.s6)),
      ],
    );
  }
}

/// A bento tile: a colour, a bold title, a short line, an optional chip and a character peeking from the
/// bottom-right corner in a small ringed circle.
class _BentoTile extends StatelessWidget {
  const _BentoTile({
    super.key,
    required this.color,
    required this.foreground,
    required this.title,
    required this.subtitle,
    required this.slot,
    required this.onTap,
    this.chip,
    this.wide = false,
  });

  final Color color;
  final Color foreground;
  final String title;
  final String subtitle;
  final IllustrationSlot slot;
  final VoidCallback onTap;
  final String? chip;
  final bool wide;

  @override
  Widget build(BuildContext context) {
    final peek = wide ? 150.0 : 96.0;
    return Semantics(
      button: true,
      label: [title, subtitle, ?chip].join(', '),
      excludeSemantics: true,
      child: Material(
        color: color,
        borderRadius: BorderRadius.circular(24),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: wide ? 150 : 130),
            child: Stack(
              children: [
                Positioned(
                  right: wide ? -18 : -22,
                  bottom: wide ? -30 : -22,
                  child: AvatarCircle(slot: slot, size: peek),
                ),
                Padding(
                  padding: const EdgeInsets.all(12),
                  child: FractionallySizedBox(
                    widthFactor: wide ? 0.58 : 0.75,
                    alignment: Alignment.topLeft,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (chip != null) ...[
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                            decoration: BoxDecoration(
                              color: const Color(0xFFF5B544),
                              borderRadius: BorderRadius.circular(999),
                            ),
                            child: Text(
                              chip!,
                              style: const TextStyle(
                                fontFamily: AppFonts.body,
                                fontSize: 11.5,
                                fontWeight: FontWeight.w700,
                                color: BlockInk.ink,
                              ),
                            ),
                          ),
                          const SizedBox(height: 8),
                        ],
                        Text(title, style: AppText.display(foreground, wide ? 22 : 17, height: 1)),
                        SizedBox(height: wide ? 14 : 22),
                        Text(
                          subtitle,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontFamily: AppFonts.body,
                            fontSize: 11.5,
                            fontWeight: FontWeight.w600,
                            color: foreground,
                          ),
                        ),
                      ],
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

class _OfferCard extends StatelessWidget {
  const _OfferCard({required this.banner});

  final HomeBanner banner;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SizedBox(
      width: 260,
      child: SectionBox(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (banner.badge != null) Sticker(label: banner.badge!, tone: StickerTone.mango),
            const SizedBox(height: FreshSpacing.s2),
            Text(banner.title, style: theme.textTheme.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
            if (banner.subtitle != null)
              Text(banner.subtitle!, style: theme.textTheme.bodySmall, maxLines: 2, overflow: TextOverflow.ellipsis),
          ],
        ),
      ),
    );
  }
}
