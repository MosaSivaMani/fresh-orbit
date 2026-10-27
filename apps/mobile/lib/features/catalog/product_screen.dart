// Product detail (D88): a teal-soft hero, the cut choice as tiles, a sticky bar with the − n + stepper and Add to
// cart. Variant and preparation choice (with yield note and the location's preparation charge),
// quantity stepper bounded by the server's min / max / step / maxOrderableNow, and add to cart.
//
// Prices: the screen shows the server's unit price and preparation charge, and for the chosen size, cut and
// quantity the server's estimate and maximum (GET /price-quote); the app computes none of them. A sold-out
// size offers "Notify me", a back-in-stock alert at the store that serves the pincode (POST /me/stock-alerts).

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/cart.dart';
import '../../api/models/catalog.dart';
import '../../api/models/common.dart';
import '../../core/format.dart';
import '../../state/account_state.dart';
import '../../state/cart_state.dart';
import '../../state/catalog_state.dart';
import '../../state/core_providers.dart';
import '../../state/location_state.dart';
import '../../state/session.dart';
import '../../widgets/bold.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

class ProductScreen extends ConsumerStatefulWidget {
  const ProductScreen({super.key, required this.slug});

  final String slug;

  @override
  ConsumerState<ProductScreen> createState() => _ProductScreenState();
}

class _ProductScreenState extends ConsumerState<ProductScreen> {
  String? _variantId;
  String? _prepCode;

  /// Preparation sent to the server so `maxOrderableNow` matches it (null = server default).
  String? _requestedPrep;
  Quantity? _quantity;
  bool _adding = false;
  String? _quantityError;
  ProductDetail? _last;
  bool _notifying = false;

  /// Sizes the customer asked to hear about in this visit.
  final Set<String> _alerted = {};

  Variant _variant(ProductDetail d) => d.variants.firstWhere((v) => v.id == _variantId, orElse: () => d.variants.first);

  String? _prep(Variant v) {
    final chosen = v.preparations.where((p) => p.code == _prepCode && p.chargePaise != null);
    if (chosen.isNotEmpty) return chosen.first.code;
    return v.defaultPreparation?.code;
  }

  Quantity _qty(Variant v) {
    final q = _quantity;
    return q != null && q.unit == v.minOrder.unit ? q : v.minOrder;
  }

  CartLine? _cartLine(Variant v, String? prep) {
    final lines = ref.watch(cartProvider).value?.lines ?? const <CartLine>[];
    for (final l in lines) {
      if (l.variantId == v.id && l.preparation?.code == prep) return l;
    }
    return null;
  }

  Future<void> _add(ProductDetail d, Variant v, String? prep, Quantity qty) async {
    setState(() {
      _adding = true;
      _quantityError = null;
    });
    try {
      final cart = await ref
          .read(cartProvider.notifier)
          .add(AddCartItem(variantId: v.id, preparationCode: prep, quantity: qty));
      if (!mounted) return;
      final line = cart.lines.where((l) => l.variantId == v.id && l.preparation?.code == prep).firstOrNull;
      final text = line == null
          ? 'Added to cart.'
          : 'Added. Estimated ${formatRupees(line.estimatedLineTotalPaise)}, at most ${formatRupees(line.maxLineTotalPaise)}.';
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(text),
            action: SnackBarAction(label: 'View cart', onPressed: () => context.go('/cart')),
          ),
        );
    } on ApiError catch (e) {
      if (!mounted) return;
      if (e.code == 'LOCATION_REQUIRED' || e.code == 'PINCODE_NOT_SERVICEABLE') {
        await context.push('/location?change=1');
        return;
      }
      final field = e.fieldMessage('quantity.value') ?? e.fieldMessage('quantity');
      if (field != null || e.code.startsWith('QUANTITY_')) {
        setState(() => _quantityError = field ?? e.message);
      } else {
        showErrorSnack(context, e);
      }
    } finally {
      if (mounted) setState(() => _adding = false);
    }
  }

  Future<void> _notify(ProductDetail d, Variant v) async {
    if (!ref.read(sessionProvider).signedIn) {
      await context.push(Uri(path: '/login', queryParameters: {'from': '/product/${d.slug}'}).toString());
      return;
    }
    setState(() => _notifying = true);
    try {
      final locationId = (await ref.read(currentServiceabilityProvider.future))?.locationId;
      if (!mounted) return;
      if (locationId == null) {
        await context.push('/location?change=1');
        return;
      }
      await ref.read(accountRepositoryProvider).addStockAlert(variantId: v.id, fulfillmentLocationId: locationId);
      if (!mounted) return;
      setState(() => _alerted.add(v.id));
      showInfoSnack(context, 'We will tell you when it is back.');
    } on ApiError catch (e) {
      if (!mounted) return;
      if (e.code == 'STOCK_AVAILABLE') ref.invalidate(productDetailProvider(ProductRequest(widget.slug, _requestedPrep)));
      showErrorSnack(context, e);
    } finally {
      if (mounted) setState(() => _notifying = false);
    }
  }

  Future<void> _toggleWishlist(ProductDetail d) async {
    if (!ref.read(sessionProvider).signedIn) {
      await context.push(Uri(path: '/login', queryParameters: {'from': '/product/${d.slug}'}).toString());
      return;
    }
    try {
      await ref.read(wishlistProvider.notifier).toggle(d.id);
    } catch (e) {
      if (mounted) showErrorSnack(context, e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = productDetailProvider(ProductRequest(widget.slug, _requestedPrep));
    final async = ref.watch(provider);
    if (async.hasValue) _last = async.value;
    final detail = _last;

    if (detail == null) {
      return Scaffold(
        appBar: const PageHeader.plain(''),
        body: async.hasError
            ? ErrorState(error: async.error!, onRetry: () => ref.invalidate(provider))
            : const LoadingSemantics(
                child: Padding(
                  padding: FreshLayout.list,
                  child: Column(
                    children: [
                      FreshPictureFrame(child: SkeletonBox(height: double.infinity, radius: FreshLayout.heroRadius)),
                      SizedBox(height: FreshLayout.sectionGap),
                      SkeletonBox(height: 24),
                      SizedBox(height: FreshSpacing.s2),
                      SkeletonBox(height: 20, width: 160),
                    ],
                  ),
                ),
              ),
      );
    }
    if (detail.variants.isEmpty) {
      return Scaffold(
        appBar: PageHeader.plain(detail.name),
        body: const EmptyState(slot: IllustrationSlot.outOfStock, title: 'Not available right now'),
      );
    }

    final theme = Theme.of(context);
    final c = illustratedColors(context);
    final v = _variant(detail);
    final prep = _prep(v);
    final qty = _qty(v);
    final maxQty = v.availability?.maxOrderableNow ?? v.maxOrder;
    final line = _cartLine(v, prep);
    final wished = ref.watch(wishlistIdsProvider).contains(detail.id) || (detail.isWishlisted ?? false);
    final selectedPrep = v.preparations.where((p) => p.code == prep).firstOrNull;
    final locationRequired = detail.locationRequired || v.unitPricePaise == null;
    final heroSticker = v.availability != null && v.availability!.status != 'IN_STOCK'
        ? Sticker(
            label: v.availability!.status == 'LOW_STOCK' ? 'Low stock' : 'Sold out',
            tone: StickerTone.coral,
            tilt: -0.011,
          )
        : detail.sourceType == null
        ? null
        : Sticker(label: humanizeCode(detail.sourceType!), tone: StickerTone.mango, tilt: -0.011);

    return Scaffold(
      appBar: PageHeader.plain(
        '',
        actions: [
          PaperCircleButton(
            key: const Key('productSave'),
            icon: wished ? Icons.favorite_rounded : Icons.favorite_border_rounded,
            label: wished ? 'Remove from saved' : 'Save for later',
            onTap: () => _toggleWishlist(detail),
          ),
          PaperCircleButton(
            key: const Key('productCart'),
            icon: Icons.shopping_cart_outlined,
            label: 'Open cart',
            onTap: () => context.go('/cart'),
          ),
        ],
      ),
      body: ListView(
        padding: EdgeInsets.zero,
        children: [
          // Hero: the picture in the 3:2 frame on teal-soft inside the gutters, with a sticker (D193).
          Padding(
            padding: FreshLayout.page,
            child: FreshPictureFrame(
              key: const Key('productHero'),
              color: c.tealSoft,
              borderRadius: BorderRadius.circular(FreshLayout.heroRadius),
              child: Stack(
                children: [
                  Positioned.fill(child: _Gallery(images: detail.images)),
                  if (heroSticker != null) Positioned(left: 14, bottom: 12, child: heroSticker),
                ],
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 14, FreshLayout.gutter, FreshLayout.sectionGap),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Semantics(header: true, child: Text(detail.name, style: AppText.display(c.ink, 26, height: 1.05))),
                if (detail.localNames.isNotEmpty)
                  Text(
                    'Also called ${detail.localNames.join(', ')}',
                    style: theme.textTheme.bodyMedium?.copyWith(color: c.muted),
                  ),
                const SizedBox(height: FreshSpacing.s2),
                Wrap(
                  spacing: FreshSpacing.s2,
                  runSpacing: FreshSpacing.s1,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    if (v.unitPricePaise != null)
                      PriceTag(
                        paise: v.unitPricePaise!,
                        compareAtPaise: v.compareAtPaise,
                        perUnit: v.sellUnit,
                        style: AppText.price(c.ink, size: 22),
                      ),
                    if (v.availability != null) AvailabilityBadge(status: v.availability!.status),
                    if (async.isLoading) const FreshBadge(label: 'Updating…'),
                  ],
                ),
                if (locationRequired) ...[
                  const SizedBox(height: FreshSpacing.s2),
                  const InfoBanner(
                    text: 'Set your pincode to see the price and availability.',
                    tone: BannerTone.warning,
                  ),
                ],
                if (v.availability?.message != null) ...[
                  const SizedBox(height: FreshSpacing.s2),
                  InfoBanner(text: v.availability!.message!, tone: BannerTone.warning),
                ],
                if (v.availability?.status == 'OUT_OF_STOCK' || v.availability?.status == 'NOT_SOLD_HERE') ...[
                  const SizedBox(height: FreshSpacing.s3),
                  _SoldOutCard(notSoldHere: v.availability!.status == 'NOT_SOLD_HERE'),
                ],
                const SizedBox(height: FreshLayout.cardGap),
                const _Facts(),
                if (detail.variants.length > 1) ...[
                  const SizedBox(height: FreshLayout.sectionGap),
                  const PageTitle(text: 'Pick a', brushed: 'size', size: 19),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final option in detail.variants)
                        ChoiceChip(
                          label: Text(option.name),
                          selected: option.id == v.id,
                          onSelected: (_) => setState(() {
                            _variantId = option.id;
                            _quantity = null;
                            _quantityError = null;
                          }),
                        ),
                    ],
                  ),
                ] else ...[
                  const SizedBox(height: FreshSpacing.s2),
                  Text(v.name, style: theme.textTheme.bodyMedium?.copyWith(color: c.muted)),
                ],
                if (v.preparations.isNotEmpty) ...[
                  const SizedBox(height: FreshLayout.sectionGap),
                  const PageTitle(text: 'Choose your', brushed: 'cut', size: 19),
                  const SizedBox(height: 8),
                  GridView.count(
                    crossAxisCount: 2,
                    shrinkWrap: true,
                    physics: const NeverScrollableScrollPhysics(),
                    mainAxisSpacing: FreshLayout.cardGap,
                    crossAxisSpacing: FreshLayout.cardGap,
                    childAspectRatio: 2.6,
                    children: [
                      for (final p in v.preparations)
                        _CutTile(
                          name: p.name,
                          detail: preparationChargeText(p),
                          selected: p.code == prep,
                          onTap: p.chargePaise == null
                              ? null
                              : () => setState(() {
                                  _prepCode = p.code;
                                  _requestedPrep = p.code;
                                  _quantityError = null;
                                }),
                        ),
                    ],
                  ),
                  if (selectedPrep != null) ...[
                    const SizedBox(height: FreshSpacing.s2),
                    Text(formatYieldNote(selectedPrep.yieldBp), style: theme.textTheme.bodySmall),
                  ],
                ],
                if (_quantityError != null)
                  Padding(
                    padding: const EdgeInsets.only(top: FreshSpacing.s2),
                    child: Text(
                      _quantityError!,
                      style: theme.textTheme.bodySmall?.copyWith(color: freshColors(context).dangerText),
                    ),
                  ),
                const SizedBox(height: FreshSpacing.s2),
                Text(
                  'Order ${formatQuantity(v.minOrder)} to ${formatQuantity(maxQty)}, in steps of ${formatQuantity(v.orderStep)} (net weight).',
                  style: theme.textTheme.bodySmall?.copyWith(color: c.muted),
                ),
                if (!locationRequired && (v.availability?.canAdd ?? true)) ...[
                  const SizedBox(height: 10),
                  _Estimate(request: PriceQuoteRequest(variantId: v.id, preparation: prep, quantity: qty)),
                ],
                if (detail.netWeightNote.isNotEmpty) ...[
                  const SizedBox(height: 10),
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.symmetric(horizontal: FreshLayout.cardGap, vertical: 8),
                    decoration: BoxDecoration(
                      color: c.coralSoft,
                      borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
                    ),
                    child: Text(
                      detail.netWeightNote,
                      style: theme.textTheme.bodySmall?.copyWith(color: c.coralText, fontWeight: FontWeight.w600),
                    ),
                  ),
                ],
                if (line != null) ...[
                  const SizedBox(height: FreshSpacing.s3),
                  InfoBanner(
                    tone: BannerTone.success,
                    title: 'In your cart: ${formatQuantity(line.quantity)}',
                    text:
                        'Estimated ${formatRupees(line.estimatedLineTotalPaise)}. You pay at most '
                        '${formatRupees(line.maxLineTotalPaise)} (up to ${formatQuantity(line.maxChargeableQuantity)}).',
                  ),
                ],
                const SizedBox(height: FreshSpacing.s3),
                if (detail.description != null)
                  ExpansionTile(
                    tilePadding: EdgeInsets.zero,
                    title: const Text('About'),
                    initiallyExpanded: true,
                    children: [Align(alignment: Alignment.centerLeft, child: Text(detail.description!))],
                  ),
                if (detail.storageInstructions != null)
                  ExpansionTile(
                    tilePadding: EdgeInsets.zero,
                    title: const Text('Storage and cooking'),
                    children: [Align(alignment: Alignment.centerLeft, child: Text(detail.storageInstructions!))],
                  ),
                if (detail.sourceRegion != null)
                  ExpansionTile(
                    tilePadding: EdgeInsets.zero,
                    title: const Text('Sourcing'),
                    children: [Align(alignment: Alignment.centerLeft, child: Text('From ${detail.sourceRegion}'))],
                  ),
              ],
            ),
          ),
        ],
      ),
      bottomNavigationBar: StickyActionBar(
        leading: locationRequired || !(v.availability?.canAdd ?? true)
            ? null
            : CompactStepper(
                large: true,
                label: formatQuantity(qty),
                itemName: detail.name,
                onDecrease: qty.value - v.orderStep.value >= v.minOrder.value
                    ? () => setState(() {
                        _quantity = qty.withValue(qty.value - v.orderStep.value);
                        _quantityError = null;
                      })
                    : null,
                onIncrease: qty.value + v.orderStep.value <= maxQty.value
                    ? () => setState(() {
                        _quantity = qty.withValue(qty.value + v.orderStep.value);
                        _quantityError = null;
                      })
                    : null,
              ),
        action: _AddAction(
          variant: v,
          locationRequired: locationRequired,
          adding: _adding,
          onAdd: () => _add(detail, v, prep, qty),
          onSetPincode: () => context.push('/location?change=1'),
          notifying: _notifying,
          notified: _alerted.contains(v.id),
          onNotify: () => _notify(detail, v),
        ),
      ),
    );
  }
}

/// Three fixed promises of the shop, as small tiles.
class _Facts extends StatelessWidget {
  const _Facts();

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    Widget fact(IconData icon, String text) => FreshCard(
      color: c.paper,
      shadow: cardShadow(context),
      radius: FreshLayout.tileRadius,
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 4),
      child: Column(
          children: [
            Icon(icon, size: 17, color: c.tealText),
            const SizedBox(height: 2),
            Text(
              text,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontFamily: AppFonts.body,
                fontSize: 10.5,
                height: 1.2,
                fontWeight: FontWeight.w700,
                color: c.ink,
              ),
            ),
          ],
        ),
    );
    return FreshColumns(
      stretch: true,
      children: [
        fact(Icons.ac_unit_rounded, 'Kept cold'),
        fact(Icons.content_cut_rounded, 'Cut your way'),
        fact(Icons.scale_outlined, 'Net weight'),
      ],
    );
  }
}

/// One cut choice: its name and the location's charge; the chosen one is teal.
class _CutTile extends StatelessWidget {
  const _CutTile({required this.name, required this.detail, required this.selected, required this.onTap});

  final String name;
  final String detail;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      button: true,
      selected: selected,
      enabled: onTap != null,
      label: '$name, $detail',
      excludeSemantics: true,
      child: Material(
        color: selected ? c.tealSoft : c.paper,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
          side: BorderSide(color: selected ? c.tealText : c.line, width: selected ? 2 : 1.5),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: FreshLayout.cardGap, vertical: 6),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text(
                  name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontFamily: AppFonts.body,
                    fontSize: 12.5,
                    fontWeight: FontWeight.w700,
                    color: onTap == null ? c.muted : c.ink,
                  ),
                ),
                Text(
                  detail,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontFamily: AppFonts.body,
                    fontSize: 10.5,
                    fontWeight: FontWeight.w500,
                    color: c.muted,
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

class _AddAction extends StatelessWidget {
  const _AddAction({
    required this.variant,
    required this.locationRequired,
    required this.adding,
    required this.onAdd,
    required this.onSetPincode,
    required this.notifying,
    required this.notified,
    required this.onNotify,
  });

  final Variant variant;
  final bool locationRequired;
  final bool adding;
  final VoidCallback onAdd;
  final VoidCallback onSetPincode;
  final bool notifying;
  final bool notified;
  final VoidCallback onNotify;

  @override
  Widget build(BuildContext context) {
    if (locationRequired) return TealActionButton(label: 'Set pincode to buy', onPressed: onSetPincode);
    final availability = variant.availability;
    if (availability != null && !availability.canAdd) {
      if (availability.status == 'NOT_SOLD_HERE') {
        return const TealActionButton(label: 'Not sold in your area', onPressed: null);
      }
      return TealActionButton(
        key: const Key('notifyMe'),
        label: notified ? 'We will tell you' : 'Notify me',
        loading: notifying,
        onPressed: notified ? null : onNotify,
      );
    }
    return TealActionButton(key: const Key('addToCart'), label: 'Add to cart', loading: adding, onPressed: onAdd);
  }
}

class _Gallery extends StatefulWidget {
  const _Gallery({required this.images});

  final List<ProductImage> images;

  @override
  State<_Gallery> createState() => _GalleryState();
}

class _GalleryState extends State<_Gallery> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final images = widget.images;
    if (images.length <= 1) {
      return ProductImageView(
        url: images.firstOrNull?.url,
        alt: images.firstOrNull?.altText,
        aspectRatio: FreshLayout.pictureAspect,
      );
    }
    return Semantics(
      label: 'Image ${_index + 1} of ${images.length}',
      child: PageView.builder(
        itemCount: images.length,
        onPageChanged: (i) => setState(() => _index = i),
        itemBuilder: (_, i) =>
            ProductImageView(url: images[i].url, alt: images[i].altText, aspectRatio: FreshLayout.pictureAspect),
      ),
    );
  }
}

/// Large product image with alt text from the server.
class ProductImageView extends StatelessWidget {
  const ProductImageView({super.key, required this.url, this.alt, this.aspectRatio = 4 / 3});

  final String? url;
  final String? alt;
  final double aspectRatio;

  @override
  Widget build(BuildContext context) => ProductPhoto(
    url: url,
    semanticLabel: alt == null || alt!.isEmpty ? null : alt,
    aspectRatio: aspectRatio,
    radius: 0,
    transparent: true,
  );
}

/// Sold out (or not sold at the store for this pincode) for the chosen size: the OUT_OF_STOCK character and
/// what to do next.
class _SoldOutCard extends StatelessWidget {
  const _SoldOutCard({required this.notSoldHere});

  final bool notSoldHere;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SectionBox(
      child: Padding(
        padding: const EdgeInsets.all(FreshSpacing.s1),
        child: Row(
          children: [
            const AvatarCircle(slot: IllustrationSlot.outOfStock, size: 88),
            const SizedBox(width: FreshSpacing.s3),
            Expanded(
              child: Semantics(
                liveRegion: true,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      notSoldHere ? 'Not sold in your area' : 'Sold out right now',
                      style: theme.textTheme.titleMedium,
                    ),
                    const SizedBox(height: FreshSpacing.s1),
                    Text(
                      notSoldHere
                          ? 'The store that delivers to your pincode does not sell this size.'
                          : 'Save it to your wishlist and check again later.',
                      style: theme.textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The server's estimate for the chosen size, cut and quantity (GET /price-quote). Hidden when the server
/// gives none (no price at this store, or the quote is refused); the cart prices the line again when added.
class _Estimate extends ConsumerWidget {
  const _Estimate({required this.request});

  final PriceQuoteRequest request;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final quote = ref.watch(priceQuoteProvider(request));
    return switch (quote) {
      AsyncData(:final value) => PriceEstimateRow(
        key: const Key('priceQuote'),
        estimate: 'Estimated ${formatRupees(value.estimatedTotalPaise)} for ${formatQuantity(value.quantity)}',
        atMost: value.maxChargeablePaise == null
            ? null
            : value.maxChargeableQuantity == null
            ? 'You pay at most ${formatRupees(value.maxChargeablePaise!)}.'
            : 'You pay at most ${formatRupees(value.maxChargeablePaise!)} '
                  '(up to ${formatQuantity(value.maxChargeableQuantity!)}).',
      ),
      AsyncError() => const SizedBox.shrink(),
      _ => const SkeletonBox(height: 48),
    };
  }
}
