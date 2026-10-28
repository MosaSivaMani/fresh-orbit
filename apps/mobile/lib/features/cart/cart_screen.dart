// Cart (D88): the server's priced cart (§4.6, §5) with − n + steppers per line, the coupon strip, delivery slot
// buttons, the bill and a sticky bar to checkout. Every change is sent to the server and the returned cart
// replaces what is shown; line totals, discounts, fees and totals are never computed here. An empty cart is the
// coral block.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/cart.dart';
import '../../api/models/catalog.dart';
import '../../api/models/locations.dart';
import '../../core/format.dart';
import '../../state/cart_state.dart';
import '../../state/catalog_state.dart';
import '../../state/checkout_state.dart';
import '../../state/location_state.dart';
import '../../state/session.dart';
import '../../widgets/bold.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';
import 'totals_card.dart';

class CartScreen extends ConsumerWidget {
  const CartScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final cart = ref.watch(cartProvider);
    final value = cart.value;
    if (value != null && value.isEmpty && !cart.hasError) {
      return Scaffold(backgroundColor: BlockTone.coral.background(c), body: const _EmptyCart());
    }
    return Scaffold(
      appBar: const PageHeader(text: 'Your', brushed: 'cart', back: false),
      body: SafeArea(
        top: false,
        bottom: false,
        child: RefreshIndicator(
          onRefresh: () => ref.read(cartProvider.notifier).reload(),
          child: AsyncValueView<Cart>(
            value: cart,
            onRetry: () => ref.invalidate(cartProvider),
            data: (c) => _CartBody(cart: c),
          ),
        ),
      ),
      bottomNavigationBar: switch (value) {
        final Cart v when !v.isEmpty && v.totals != null => StickyActionBar(
          leading: const _PayHint(),
          action: TealActionButton(
            key: const Key('checkoutButton'),
            label: 'Checkout',
            amount: formatRupees(v.totals!.estimatedTotalPaise),
            semanticLabel: 'Checkout, estimated ${formatRupees(v.totals!.estimatedTotalPaise)}',
            onPressed: v.canCheckout ? () => context.push('/checkout') : null,
          ),
        ),
        _ => null,
      },
    );
  }
}

class _EmptyCart extends StatelessWidget {
  const _EmptyCart();

  @override
  Widget build(BuildContext context) => ColorBlockScreen(
    tone: BlockTone.coral,
    eyebrow: 'Your cart',
    heading: const [HeadingLine('Basket’s'), HeadingLine('empty', script: true)],
    slot: IllustrationSlot.emptyCart,
    seaFloor: true,
    sticker: const StickerSpot(
      right: 26,
      top: 36,
      child: Sticker(label: 'Fresh today', tone: StickerTone.mango, tilt: 0.017),
    ),
    children: [
      const SheetTitle('Nothing in here yet', header: true),
      const SheetNote('Fish, prawns and crabs are waiting.'),
      SizedBox(
        width: double.infinity,
        child: FilledButton(onPressed: () => context.go('/shop'), child: const Text('See today’s catch')),
      ),
    ],
  );
}

/// "Pay on delivery · Cash / UPI": the payment the app offers today; the method is chosen at checkout.
class _PayHint extends StatelessWidget {
  const _PayHint();

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('PAY ON DELIVERY', style: AppText.eyebrow(c.muted, size: 10)),
        Text(
          'Cash / UPI',
          style: TextStyle(fontFamily: AppFonts.body, fontSize: 13, fontWeight: FontWeight.w800, color: c.ink),
        ),
      ],
    );
  }
}

class _CartBody extends ConsumerWidget {
  const _CartBody({required this.cart});

  final Cart cart;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final totals = cart.totals;
    final pincode = cart.pincode ?? ref.watch(pincodeProvider);
    return ListView(
      padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, FreshLayout.sectionGap),
      children: [
        Padding(
          padding: const EdgeInsets.only(bottom: FreshLayout.cardGap),
          child: Text(
            [
              '${cart.lineCount} ${cart.lineCount == 1 ? 'item' : 'items'}',
              if (pincode != null) 'delivering to $pincode',
            ].join(' · '),
            key: const Key('cartSummary'),
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: c.muted),
          ),
        ),
        SectionBox(
          padding: const EdgeInsets.symmetric(horizontal: FreshLayout.cardGap, vertical: 4),
          child: Column(
            children: [
              for (var i = 0; i < cart.lines.length; i++) ...[
                if (i > 0) Divider(color: c.line, height: 1),
                _CartLineRow(line: cart.lines[i], index: i),
              ],
            ],
          ),
        ),
        const SizedBox(height: FreshLayout.cardGap),
        const _CouponStrip(),
        if (pincode != null) ...[const SizedBox(height: FreshLayout.cardGap), _CartSlots(pincode: pincode)],
        const SizedBox(height: FreshLayout.cardGap),
        for (final b in cart.checkoutBlockers)
          Padding(
            padding: const EdgeInsets.only(bottom: FreshLayout.cardGap),
            child: InfoBanner(tone: BannerTone.danger, text: b.message.isEmpty ? humanizeCode(b.code) : b.message),
          ),
        if (totals != null) ...[ShortfallBanners(totals: totals), TotalsCard(totals: totals)],
        for (final m in cart.messages)
          Padding(
            padding: const EdgeInsets.only(top: FreshLayout.cardGap),
            child: InfoBanner(text: m),
          ),
      ],
    );
  }
}

class _CartLineRow extends ConsumerStatefulWidget {
  const _CartLineRow({required this.line, required this.index});

  final CartLine line;
  final int index;

  @override
  ConsumerState<_CartLineRow> createState() => _CartLineRowState();
}

class _CartLineRowState extends ConsumerState<_CartLineRow> {
  bool _pending = false;

  Future<void> _run(Future<void> Function() op) async {
    if (_pending) return;
    setState(() => _pending = true);
    try {
      await op();
    } catch (e) {
      if (mounted) showErrorSnack(context, e);
    } finally {
      if (mounted) setState(() => _pending = false);
    }
  }

  Future<void> _remove() => _run(() => ref.read(cartProvider.notifier).remove(widget.line.id));

  Future<void> _step({required bool up}) => _run(() => ref.read(cartProvider.notifier).stepLine(widget.line, up: up));

  /// The cuts the server sells for this line's size at this pincode; the server re-prices the line.
  Future<void> _changeCut() async {
    final code = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _ChangeCutSheet(line: widget.line),
    );
    if (code == null || code == widget.line.preparation?.code) return;
    await _run(() => ref.read(cartProvider.notifier).changePreparation(widget.line.id, code));
  }

  @override
  Widget build(BuildContext context) {
    final line = widget.line;
    final c = illustratedColors(context);
    final theme = Theme.of(context);
    final muted = theme.textTheme.bodySmall?.copyWith(color: c.muted, fontSize: 11);
    final tints = softTints(c);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Semantics(
                button: true,
                label: 'Open ${line.productName}',
                excludeSemantics: true,
                child: InkWell(
                  key: ValueKey('cartLinePhoto-${line.id}'),
                  borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
                  onTap: () => context.push('/product/${line.productSlug}'),
                  child: Container(
                    width: 54,
                    height: 54,
                    decoration: BoxDecoration(
                      color: tints[widget.index % tints.length],
                      borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
                    ),
                    clipBehavior: Clip.antiAlias,
                    child: ProductPhoto(url: line.imageUrl, radius: 0, transparent: true),
                  ),
                ),
              ),
              const SizedBox(width: FreshLayout.cardGap),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(line.productName, style: AppText.display(c.ink, 15)),
                    Text(
                      [line.variantName, if (line.preparation != null) line.preparation!.name].join(' · '),
                      style: muted,
                    ),
                    if (line.preparation != null)
                      InkWell(
                        key: ValueKey('changeCut-${line.id}'),
                        borderRadius: BorderRadius.circular(8),
                        onTap: _pending ? null : _changeCut,
                        child: Padding(
                          padding: const EdgeInsets.symmetric(vertical: 4),
                          child: Text(
                            'Change cut ›',
                            style: TextStyle(
                              fontFamily: AppFonts.body,
                              fontSize: 12.5,
                              fontWeight: FontWeight.w700,
                              color: c.tealDeep,
                            ),
                          ),
                        ),
                      ),
                    const SizedBox(height: 2),
                    PriceTag(
                      paise: line.estimatedLineTotalPaise,
                      prefix: 'Estimated',
                      style: AppText.price(c.ink, size: 14),
                    ),
                    if (line.maxLineTotalPaise != line.estimatedLineTotalPaise)
                      Text(
                        'At most ${formatRupees(line.maxLineTotalPaise)} for up to ${formatQuantity(line.maxChargeableQuantity)}',
                        style: muted,
                      ),
                  ],
                ),
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  CompactStepper(
                    label: formatQuantity(line.quantity),
                    itemName: line.productName,
                    pending: _pending,
                    onDecrease: () => _step(up: false),
                    onIncrease: () => _step(up: true),
                  ),
                  IconButton(
                    tooltip: 'Remove ${line.productName}',
                    visualDensity: VisualDensity.compact,
                    icon: Icon(Icons.delete_outline_rounded, size: 19, color: c.muted),
                    onPressed: _pending ? null : _remove,
                  ),
                ],
              ),
            ],
          ),
          if (line.availability != null && line.availability!.status != 'IN_STOCK') ...[
            const SizedBox(height: 8),
            InfoBanner(
              tone: line.availability!.canAdd ? BannerTone.warning : BannerTone.danger,
              text: line.availability!.message ?? humanizeCode(line.availability!.status),
            ),
          ],
          for (final issue in line.issues) ...[
            const SizedBox(height: 8),
            InfoBanner(tone: BannerTone.danger, text: issue.message.isEmpty ? humanizeCode(issue.code) : issue.message),
          ],
        ],
      ),
    );
  }
}

/// Up to three open slots for the cart's pincode. The choice is kept and checkout starts with it when the
/// delivery address is served by the same store; the server checks it again when the order is previewed.
class _CartSlots extends ConsumerWidget {
  const _CartSlots({required this.pincode});

  final String pincode;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final slots = ref.watch(deliverySlotsProvider(pincode)).value;
    if (slots == null) return const SizedBox.shrink();
    final open = slots.items.where((s) => s.isBookable).take(3).toList();
    final chosen = ref.watch(preferredSlotProvider);
    return SectionBox(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const PageTitle(text: 'Delivery', brushed: 'slot', size: 18),
          const SizedBox(height: 8),
          if (open.isEmpty)
            Text(
              'No delivery slots are open right now.',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(color: illustratedColors(context).muted),
            )
          else
            Row(
              children: [
                for (var i = 0; i < open.length; i++) ...[
                  if (i > 0) const SizedBox(width: 8),
                  Expanded(child: _slotButton(ref, open[i], chosen)),
                ],
              ],
            ),
        ],
      ),
    );
  }

  Widget _slotButton(WidgetRef ref, DeliverySlot s, String? chosen) => SlotButton(
    key: ValueKey('cartSlot-${s.id}'),
    title: formatDayShort(s.startAt),
    subtitle: formatTimeRange(s.startAt, s.endAt),
    selected: s.id == chosen,
    onTap: () => ref.read(preferredSlotProvider.notifier).choose(s.id),
  );
}

/// The coupon strip: mango-soft with a mango edge. Applied coupons show the server's saving.
class _CouponStrip extends ConsumerStatefulWidget {
  const _CouponStrip();

  @override
  ConsumerState<_CouponStrip> createState() => _CouponStripState();
}

class _CouponStripState extends ConsumerState<_CouponStrip> {
  final _field = TextEditingController();
  String? _error;
  bool _busy = false;

  @override
  void dispose() {
    _field.dispose();
    super.dispose();
  }

  Future<void> _apply() async {
    final code = _field.text.trim().toUpperCase();
    if (code.isEmpty) {
      setState(() => _error = 'Enter a coupon code.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(cartProvider.notifier).applyCoupon(code);
      _field.clear();
    } on ApiError catch (e) {
      setState(() => _error = e.fieldMessage('code') ?? e.fieldMessage('couponCode') ?? e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _remove() async {
    setState(() => _busy = true);
    try {
      await ref.read(cartProvider.notifier).removeCoupon();
    } catch (e) {
      if (mounted) showErrorSnack(context, e);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    final coupon = ref.watch(cartProvider).value?.coupon;
    final bold = AppText.display(c.ink, 13.5);
    final small = TextStyle(fontFamily: AppFonts.body, fontSize: 12, color: c.ink);
    Widget strip(Widget child) => Container(
      padding: const EdgeInsets.fromLTRB(FreshLayout.cardGap, 8, 6, 8),
      decoration: BoxDecoration(
        color: c.mangoSoft,
        borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
        border: Border.all(color: c.mango, width: 1.5),
      ),
      child: Row(
        children: [
          Icon(Icons.local_offer_outlined, size: 18, color: c.coralText),
          const SizedBox(width: 9),
          Expanded(child: child),
        ],
      ),
    );
    if (!signedIn) {
      return InkWell(
        borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
        onTap: () => context.push('/login'),
        child: strip(Text('Sign in to apply a coupon', style: bold)),
      );
    }
    if (coupon != null) {
      final applied = coupon.status == 'APPLIED';
      return strip(
        Row(
          children: [
            Expanded(
              child: Semantics(
                liveRegion: true,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(applied ? '${coupon.code} applied' : coupon.code, style: bold),
                    Text(
                      coupon.message ??
                          (applied
                              ? 'Applied: you save ${formatRupees(coupon.discountPaise)}.'
                              : humanizeCode(coupon.status)),
                      style: small,
                    ),
                  ],
                ),
              ),
            ),
            IconButton(
              tooltip: 'Remove coupon ${coupon.code}',
              onPressed: _busy ? null : _remove,
              icon: const Icon(Icons.close_rounded, size: 18),
            ),
          ],
        ),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        strip(
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: TextField(
                  controller: _field,
                  textCapitalization: TextCapitalization.characters,
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp('[A-Za-z0-9_-]'))],
                  decoration: InputDecoration(
                    labelText: 'Coupon code',
                    errorText: _error,
                    isDense: true,
                    fillColor: c.paper,
                  ),
                  onSubmitted: (_) => _apply(),
                ),
              ),
              const SizedBox(width: 6),
              TextButton(onPressed: _busy ? null : _apply, child: const Text('Apply')),
            ],
          ),
        ),
        Align(
          alignment: Alignment.centerRight,
          child: TextButton.icon(
            key: const Key('viewOffers'),
            onPressed: _busy
                ? null
                : () => showModalBottomSheet<void>(
                    context: context,
                    isScrollControlled: true,
                    showDragHandle: true,
                    builder: (_) => const _OffersSheet(),
                  ),
            icon: const Icon(Icons.local_offer_outlined, size: 18),
            label: const Text('View offers'),
          ),
        ),
      ],
    );
  }
}

/// The coupons the customer may use in the pincode's city (GET /coupons). Apply sends the code; the cart
/// shows the server's result. The app never works out a saving from the list.
class _OffersSheet extends ConsumerStatefulWidget {
  const _OffersSheet();

  @override
  ConsumerState<_OffersSheet> createState() => _OffersSheetState();
}

class _OffersSheetState extends ConsumerState<_OffersSheet> {
  String? _applying;

  /// The server's conditions, formatted: "On orders of ₹499 or more, first order only, until 31 Oct 2026".
  static String? _condition(VisibleCoupon o) {
    final parts = [
      if (o.minOrderPaise > 0) 'On orders of ${formatRupees(o.minOrderPaise)} or more',
      if (o.firstOrderOnly) 'first order only',
      if (o.validTo != null) 'until ${formatDate(o.validTo!)}',
    ];
    return parts.isEmpty ? null : parts.join(', ');
  }

  Future<void> _apply(String code) async {
    setState(() => _applying = code);
    try {
      await ref.read(cartProvider.notifier).applyCoupon(code);
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) showErrorSnack(context, e);
    } finally {
      if (mounted) setState(() => _applying = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final offers = ref.watch(couponsProvider);
    return SafeArea(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.7),
        child: ListView(
          key: const Key('offersSheet'),
          shrinkWrap: true,
          padding: FreshLayout.list,
          children: [
            const PageTitle(text: 'Offers for', brushed: 'you', size: 22),
            const SizedBox(height: FreshLayout.cardGap),
            ...switch (offers) {
              AsyncData(:final value) when value.isEmpty => [
                const InfoBanner(text: 'There are no offers for your area right now.'),
              ],
              AsyncData(:final value) => [
                for (final o in value)
                  Padding(
                    padding: const EdgeInsets.only(bottom: FreshLayout.cardGap),
                    child: CouponTile(
                      key: ValueKey('offer-${o.code}'),
                      code: o.code,
                      description: o.description,
                      condition: _condition(o),
                      actionLabel: 'Apply',
                      busy: _applying == o.code,
                      onAction: _applying != null ? null : () => _apply(o.code),
                    ),
                  ),
              ],
              AsyncError(:final error) => [
                ErrorState(error: error, onRetry: () => ref.invalidate(couponsProvider)),
              ],
              _ => [const LoadingSemantics(child: SkeletonBox(height: 72))],
            },
          ],
        ),
      ),
    );
  }
}

/// The cuts of a cart line's size at the current pincode, from the product endpoint, each with the server's
/// charge. Picking one returns its code; the cart sends it and shows the server's re-priced line.
class _ChangeCutSheet extends ConsumerWidget {
  const _ChangeCutSheet({required this.line});

  final CartLine line;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final provider = productDetailProvider(ProductRequest(line.productSlug));
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, FreshLayout.sectionGap),
        child: Column(
          key: const Key('changeCutSheet'),
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const PageTitle(text: 'Change your', brushed: 'cut', size: 22),
            const SizedBox(height: 4),
            Text(line.productName, style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: c.muted)),
            const SizedBox(height: FreshLayout.cardGap),
            AsyncValueView<ProductDetail>(
              value: ref.watch(provider),
              onRetry: () => ref.invalidate(provider),
              loading: const Padding(
                padding: EdgeInsets.all(FreshLayout.sectionGap),
                child: Center(child: CircularProgressIndicator()),
              ),
              data: (detail) {
                final variant = detail.variants.where((v) => v.id == line.variantId).firstOrNull;
                final cuts = [...?variant?.preparations.where((p) => p.chargePaise != null)];
                if (cuts.isEmpty) {
                  return Text(
                    'No other cut is sold for this size here.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: c.muted),
                  );
                }
                return Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    for (final p in cuts)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: OptionTile(
                          key: ValueKey('cut-${p.code}'),
                          title: p.name,
                          subtitle: '${preparationChargeText(p)} · ${formatYieldNote(p.yieldBp)}',
                          subtitleLines: 2,
                          selected: p.code == line.preparation?.code,
                          onTap: () => Navigator.pop(context, p.code),
                        ),
                      ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}
