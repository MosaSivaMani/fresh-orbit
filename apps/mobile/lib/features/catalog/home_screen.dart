import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/catalog.dart';
import '../../core/format.dart';
import '../../illustrations/illustration.dart';
import '../../state/catalog_state.dart';
import '../../state/location_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/commerce.dart';
import '../../widgets/illustrated.dart';
import '../../widgets/motion.dart';
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

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(homeProvider);
    return Scaffold(
      appBar: AppBar(
        title: const PincodeChip(),
        actions: [
          IconButton(tooltip: 'Search', icon: const Icon(Icons.search), onPressed: () => context.push('/search')),
          IconButton(
            tooltip: 'Wishlist',
            icon: const Icon(Icons.favorite_border),
            onPressed: () => context.push('/wishlist'),
          ),
        ],
      ),
      body: RefreshIndicator(
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
    );
  }
}

class _HomeBody extends ConsumerWidget {
  const _HomeBody({required this.data});

  final HomeData data;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final service = ref.watch(currentServiceabilityProvider).value;
    return CustomScrollView(
      slivers: [
        SliverPadding(
          padding: const EdgeInsets.all(FreshSpacing.s4),
          sliver: SliverList.list(
            children: [
              // Search field look-alike that opens the search screen.
              Semantics(
                button: true,
                label: 'Search fish, prawns, crabs',
                excludeSemantics: true,
                child: InkWell(
                  onTap: () => context.push('/search'),
                  borderRadius: BorderRadius.circular(AppRadius.field),
                  child: InputDecorator(
                    decoration: const InputDecoration(prefixIcon: Icon(Icons.search)),
                    child: Text(
                      'Search fish, prawns, crabs…',
                      style: theme.textTheme.bodyLarge?.copyWith(color: freshColors(context).textMuted),
                    ),
                  ),
                ),
              ),
              const SizedBox(height: FreshSpacing.s4),
              const _HomeHero(),
              const SizedBox(height: FreshSpacing.s4),
              if (data.locationRequired)
                const InfoBanner(text: 'Set your pincode to see prices and availability.', tone: BannerTone.warning)
              else if (service?.nextSlot != null)
                InfoBanner(
                  tone: BannerTone.success,
                  text: 'Next delivery: ${formatSlotWindow(service!.nextSlot!.startAt, service.nextSlot!.endAt)}',
                ),
              if (data.featuredCategories.isNotEmpty) ...[
                const SizedBox(height: FreshSpacing.s5),
                Semantics(header: true, child: Text('Categories', style: theme.textTheme.titleLarge)),
                const SizedBox(height: FreshSpacing.s2),
                Wrap(
                  spacing: FreshSpacing.s2,
                  runSpacing: FreshSpacing.s2,
                  children: [
                    for (final c in data.featuredCategories)
                      ActionChip(
                        label: Text(c.name),
                        onPressed: () =>
                            context.go(Uri(path: '/shop', queryParameters: {'category': c.slug}).toString()),
                      ),
                  ],
                ),
              ],
              if (data.banners.isNotEmpty) ...[
                const SizedBox(height: FreshSpacing.s5),
                SizedBox(
                  height: 140,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemCount: data.banners.length,
                    separatorBuilder: (_, _) => const SizedBox(width: FreshSpacing.s3),
                    itemBuilder: (context, i) => _OfferCard(banner: data.banners[i]),
                  ),
                ),
              ],
              const SizedBox(height: FreshSpacing.s6),
              Semantics(header: true, child: Text('Why Fresh Orbit', style: theme.textTheme.titleLarge)),
              const SizedBox(height: FreshSpacing.s2),
              const _Features(),
              const SizedBox(height: FreshSpacing.s5),
              Semantics(header: true, child: Text('Popular today', style: theme.textTheme.titleLarge)),
            ],
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
            padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s4),
            sliver: SliverGrid.builder(
              gridDelegate: productGridDelegate,
              itemCount: data.featuredProducts.length,
              itemBuilder: (context, i) {
                final p = data.featuredProducts[i];
                return ProductCard(product: p, onTap: () => context.push('/product/${p.slug}'));
              },
            ),
          ),
        const SliverToBoxAdapter(child: SizedBox(height: FreshSpacing.s6)),
      ],
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
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(FreshSpacing.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (banner.badge != null) Sticker(label: banner.badge!),
              const SizedBox(height: FreshSpacing.s2),
              Text(banner.title, style: theme.textTheme.titleMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
              if (banner.subtitle != null)
                Text(banner.subtitle!, style: theme.textTheme.bodySmall, maxLines: 2, overflow: TextOverflow.ellipsis),
            ],
          ),
        ),
      ),
    );
  }
}

class _HomeHero extends StatelessWidget {
  const _HomeHero();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ill = illustratedColors(context);
    return Container(
      padding: const EdgeInsets.fromLTRB(FreshSpacing.s5, FreshSpacing.s5, FreshSpacing.s3, FreshSpacing.s5),
      decoration: BoxDecoration(color: ill.tealSoft, borderRadius: BorderRadius.circular(AppRadius.hero)),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Sticker(label: 'Kept cold', tone: StickerTone.coral, icon: Icons.ac_unit_rounded),
                const SizedBox(height: FreshSpacing.s3),
                Semantics(
                  header: true,
                  child: Text('Fresh from the morning catch', style: theme.textTheme.headlineSmall),
                ),
                const SizedBox(height: FreshSpacing.s1),
                Text(
                  'Cleaned the way you cook and brought to your door.',
                  style: theme.textTheme.bodyMedium?.copyWith(color: freshColors(context).textMuted),
                ),
              ],
            ),
          ),
          const EntranceMotion(
            child: Illustration(slot: IllustrationSlot.homeHero, size: 140, excludeFromSemantics: true),
          ),
        ],
      ),
    );
  }
}

class _Features extends StatelessWidget {
  const _Features();

  static const _items = [
    (
      IllustrationSlot.featureHonestWeight,
      'Honest weight',
      'You pay for the net weight after cleaning, never for the waste.',
    ),
    (
      IllustrationSlot.featureCleanedYourWay,
      'Cleaned your way',
      'Choose whole, steaks, fillets or cleaned for every item.',
    ),
    (IllustrationSlot.featureColdChain, 'Cold all the way', 'Kept chilled from our store to your door.'),
    (IllustrationSlot.featureSameDay, 'Same-day slots', 'Order now and pick a slot today when one is open.'),
  ];

  @override
  Widget build(BuildContext context) {
    final scale = MediaQuery.textScalerOf(context).scale(1);
    return SizedBox(
      height: 300 * scale.clamp(1.0, 2.0),
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        clipBehavior: Clip.none,
        padding: const EdgeInsets.symmetric(vertical: FreshSpacing.s2),
        itemCount: _items.length,
        separatorBuilder: (_, _) => const SizedBox(width: FreshSpacing.s3),
        itemBuilder: (context, i) {
          final (slot, title, message) = _items[i];
          return FeatureCard(slot: slot, number: i + 1, title: title, message: message);
        },
      ),
    );
  }
}
