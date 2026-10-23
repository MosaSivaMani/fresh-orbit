// Product detail: variant and preparation choice (with yield note and the location's preparation charge),
// quantity stepper bounded by the server's min / max / step / maxOrderableNow, and add to cart.
//
// Prices: the screen shows the server's unit price and preparation charge. The estimated line price and the
// maximum chargeable amount come from the server's priced cart line after adding (and are shown here when the
// same variant and preparation is already in the cart). The product endpoint has no per-quantity estimate,
// so the app does not invent one (docs/api §5: a missing value is a missing API field).

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
import '../../state/session.dart';
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
        appBar: AppBar(),
        body: async.hasError
            ? ErrorState(error: async.error!, onRetry: () => ref.invalidate(provider))
            : const LoadingSemantics(
                child: Padding(
                  padding: EdgeInsets.all(FreshSpacing.s4),
                  child: Column(
                    children: [
                      SkeletonBox(height: 280, radius: FreshRadius.lg),
                      SizedBox(height: FreshSpacing.s4),
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
        appBar: AppBar(title: Text(detail.name)),
        body: const EmptyState(icon: Icons.remove_shopping_cart_outlined, title: 'Not available right now'),
      );
    }

    final theme = Theme.of(context);
    final v = _variant(detail);
    final prep = _prep(v);
    final qty = _qty(v);
    final maxQty = v.availability?.maxOrderableNow ?? v.maxOrder;
    final line = _cartLine(v, prep);
    final wished = ref.watch(wishlistIdsProvider).contains(detail.id) || (detail.isWishlisted ?? false);
    final selectedPrep = v.preparations.where((p) => p.code == prep).firstOrNull;

    return Scaffold(
      appBar: AppBar(
        title: Text(detail.name),
        actions: [
          IconButton(
            tooltip: wished ? 'Remove from wishlist' : 'Add to wishlist',
            icon: Icon(wished ? Icons.favorite : Icons.favorite_border),
            onPressed: () => _toggleWishlist(detail),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(FreshSpacing.s4),
        children: [
          _Gallery(images: detail.images),
          const SizedBox(height: FreshSpacing.s4),
          Semantics(header: true, child: Text(detail.name, style: theme.textTheme.headlineSmall)),
          if (detail.localNames.isNotEmpty)
            Text(
              'Also called ${detail.localNames.join(', ')}',
              style: theme.textTheme.bodyMedium?.copyWith(color: freshColors(context).textMuted),
            ),
          const SizedBox(height: FreshSpacing.s2),
          Wrap(
            spacing: FreshSpacing.s2,
            runSpacing: FreshSpacing.s1,
            children: [
              if (detail.sourceType != null) FreshBadge(label: humanizeCode(detail.sourceType!)),
              if (v.availability != null) AvailabilityBadge(status: v.availability!.status),
              if (async.isLoading) const FreshBadge(label: 'Updating…'),
            ],
          ),
          const SizedBox(height: FreshSpacing.s3),
          if (v.unitPricePaise != null)
            PriceTag(
              paise: v.unitPricePaise!,
              compareAtPaise: v.compareAtPaise,
              perUnit: v.sellUnit,
              style: theme.textTheme.headlineSmall,
            )
          else
            const InfoBanner(text: 'Set your pincode to see the price and availability.', tone: BannerTone.warning),
          if (v.availability?.message != null) ...[
            const SizedBox(height: FreshSpacing.s2),
            InfoBanner(text: v.availability!.message!, tone: BannerTone.warning),
          ],
          if (detail.variants.length > 1) ...[
            const SizedBox(height: FreshSpacing.s5),
            Text('Size', style: theme.textTheme.titleSmall),
            const SizedBox(height: FreshSpacing.s2),
            Wrap(
              spacing: FreshSpacing.s2,
              runSpacing: FreshSpacing.s2,
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
            Text(v.name, style: theme.textTheme.bodyMedium),
          ],
          if (v.preparations.isNotEmpty) ...[
            const SizedBox(height: FreshSpacing.s5),
            Text('Preparation', style: theme.textTheme.titleSmall),
            const SizedBox(height: FreshSpacing.s2),
            Wrap(
              spacing: FreshSpacing.s2,
              runSpacing: FreshSpacing.s2,
              children: [
                for (final p in v.preparations)
                  ChoiceChip(
                    label: Text('${p.name} · ${_chargeText(p)}'),
                    selected: p.code == prep,
                    onSelected: p.chargePaise == null
                        ? null
                        : (_) => setState(() {
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
          const SizedBox(height: FreshSpacing.s5),
          Text('Quantity (net weight)', style: theme.textTheme.titleSmall),
          const SizedBox(height: FreshSpacing.s2),
          Align(
            alignment: Alignment.centerLeft,
            child: QuantityStepper(
              value: qty,
              min: v.minOrder,
              max: maxQty,
              step: v.orderStep,
              itemName: detail.name,
              onChanged: (q) => setState(() {
                _quantity = q;
                _quantityError = null;
              }),
            ),
          ),
          if (_quantityError != null)
            Padding(
              padding: const EdgeInsets.only(top: FreshSpacing.s1),
              child: Text(
                _quantityError!,
                style: theme.textTheme.bodySmall?.copyWith(color: freshColors(context).dangerText),
              ),
            ),
          const SizedBox(height: FreshSpacing.s2),
          Text(
            'Order ${formatQuantity(v.minOrder)} to ${formatQuantity(maxQty)}, in steps of ${formatQuantity(v.orderStep)}.',
            style: theme.textTheme.bodySmall,
          ),
          if (detail.netWeightNote.isNotEmpty) ...[
            const SizedBox(height: FreshSpacing.s3),
            InfoBanner(text: detail.netWeightNote),
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
          const SizedBox(height: FreshSpacing.s4),
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
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(FreshSpacing.s4),
          child: _AddBar(
            variant: v,
            locationRequired: detail.locationRequired || v.unitPricePaise == null,
            adding: _adding,
            onAdd: () => _add(detail, v, prep, qty),
            onSetPincode: () => context.push('/location?change=1'),
          ),
        ),
      ),
    );
  }

  static String _chargeText(Preparation p) {
    final c = p.chargePaise;
    if (c == null) return 'not available here';
    if (c == 0) return 'free';
    return switch (p.chargeBasis) {
      'PER_KG' => '+${formatRupees(c)}/kg',
      'PER_ITEM' => '+${formatRupees(c)}/piece',
      _ => '+${formatRupees(c)}',
    };
  }
}

class _AddBar extends StatelessWidget {
  const _AddBar({
    required this.variant,
    required this.locationRequired,
    required this.adding,
    required this.onAdd,
    required this.onSetPincode,
  });

  final Variant variant;
  final bool locationRequired;
  final bool adding;
  final VoidCallback onAdd;
  final VoidCallback onSetPincode;

  @override
  Widget build(BuildContext context) {
    if (locationRequired) {
      return PrimaryButton(label: 'Set pincode to buy', icon: Icons.location_on_outlined, onPressed: onSetPincode);
    }
    final availability = variant.availability;
    if (availability != null && !availability.canAdd) {
      return PrimaryButton(
        label: availability.status == 'NOT_SOLD_HERE' ? 'Not sold in your area' : 'Sold out',
        onPressed: null,
      );
    }
    return PrimaryButton(
      key: const Key('addToCart'),
      label: 'Add to cart',
      icon: Icons.add_shopping_cart,
      loading: adding,
      onPressed: onAdd,
    );
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
      return ProductImageView(url: images.firstOrNull?.url, alt: images.firstOrNull?.altText);
    }
    return Column(
      children: [
        AspectRatio(
          aspectRatio: 4 / 3,
          child: PageView.builder(
            itemCount: images.length,
            onPageChanged: (i) => setState(() => _index = i),
            itemBuilder: (_, i) => ProductImageView(url: images[i].url, alt: images[i].altText, aspectRatio: 4 / 3),
          ),
        ),
        const SizedBox(height: FreshSpacing.s2),
        Semantics(label: 'Image ${_index + 1} of ${images.length}', child: const SizedBox(height: 4)),
      ],
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
  Widget build(BuildContext context) =>
      ProductPhoto(url: url, semanticLabel: alt == null || alt!.isEmpty ? null : alt, aspectRatio: aspectRatio);
}
