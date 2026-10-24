// Cart: the server's priced cart (§4.6, §5). Every change is sent to the server and the returned cart replaces
// what is shown; line totals, discounts, fees and totals are never computed here.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/cart.dart';
import '../../api/models/catalog.dart';
import '../../api/models/common.dart';
import '../../core/format.dart';
import '../../state/cart_state.dart';
import '../../state/catalog_state.dart';
import '../../state/session.dart';
import '../../theme/app_theme.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';
import 'totals_card.dart';

class CartScreen extends ConsumerWidget {
  const CartScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cart = ref.watch(cartProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Cart')),
      body: RefreshIndicator(
        onRefresh: () => ref.read(cartProvider.notifier).reload(),
        child: AsyncValueView<Cart>(
          value: cart,
          onRetry: () => ref.invalidate(cartProvider),
          data: (c) => c.isEmpty
              ? EmptyState(
                  slot: IllustrationSlot.emptyCart,
                  title: 'Your cart is empty',
                  message: 'Fresh fish, prawns and crabs are waiting.',
                  action: FilledButton(onPressed: () => context.go('/shop'), child: const Text('Browse fish')),
                )
              : _CartBody(cart: c),
        ),
      ),
      bottomNavigationBar: switch (cart.value) {
        final Cart c when !c.isEmpty && c.totals != null => SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(FreshSpacing.s4),
            child: PrimaryButton(
              key: const Key('checkoutButton'),
              label: 'Checkout · ${formatRupees(c.totals!.estimatedTotalPaise)}',
              onPressed: c.canCheckout ? () => context.push('/checkout') : null,
            ),
          ),
        ),
        _ => null,
      },
    );
  }
}

class _CartBody extends StatelessWidget {
  const _CartBody({required this.cart});

  final Cart cart;

  @override
  Widget build(BuildContext context) {
    final totals = cart.totals;
    return ListView(
      padding: const EdgeInsets.all(FreshSpacing.s4),
      children: [
        Text(
          '${cart.lineCount} ${cart.lineCount == 1 ? 'item' : 'items'}',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: FreshSpacing.s3),
        for (final line in cart.lines) ...[_CartLineTile(line: line), const SizedBox(height: FreshSpacing.s3)],
        const _CouponBox(),
        const SizedBox(height: FreshSpacing.s3),
        for (final b in cart.checkoutBlockers)
          Padding(
            padding: const EdgeInsets.only(bottom: FreshSpacing.s2),
            child: InfoBanner(tone: BannerTone.danger, text: b.message.isEmpty ? humanizeCode(b.code) : b.message),
          ),
        if (totals != null) ...[ShortfallBanners(totals: totals), TotalsCard(totals: totals)],
        for (final m in cart.messages)
          Padding(
            padding: const EdgeInsets.only(top: FreshSpacing.s2),
            child: InfoBanner(text: m),
          ),
      ],
    );
  }
}

class _CartLineTile extends ConsumerStatefulWidget {
  const _CartLineTile({required this.line});

  final CartLine line;

  @override
  ConsumerState<_CartLineTile> createState() => _CartLineTileState();
}

class _CartLineTileState extends ConsumerState<_CartLineTile> {
  bool _pending = false;

  Future<void> _run(Future<void> Function() op) async {
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

  Future<void> _changeQuantity() async {
    final q = await showModalBottomSheet<Quantity>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (_) => _QuantitySheet(line: widget.line),
    );
    if (q == null || q == widget.line.quantity) return;
    await _run(() => ref.read(cartProvider.notifier).changeQuantity(widget.line.id, q));
  }

  @override
  Widget build(BuildContext context) {
    final line = widget.line;
    final theme = Theme.of(context);
    final muted = theme.textTheme.bodySmall?.copyWith(color: freshColors(context).textMuted);
    return Dismissible(
      key: ValueKey(line.id),
      direction: DismissDirection.endToStart,
      background: Container(
        alignment: Alignment.centerRight,
        padding: const EdgeInsets.only(right: FreshSpacing.s4),
        color: freshColors(context).dangerSubtle,
        child: Icon(Icons.delete_outline, color: freshColors(context).dangerText),
      ),
      confirmDismiss: (_) async {
        await _remove();
        return false; // the server's cart decides what is shown
      },
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(FreshSpacing.s3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SizedBox(
                    width: 72,
                    child: ProductPhoto(url: line.imageUrl, radius: AppRadius.field),
                  ),
                  const SizedBox(width: FreshSpacing.s3),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(line.productName, style: theme.textTheme.titleSmall),
                        Text(
                          [line.variantName, if (line.preparation != null) line.preparation!.name].join(' · '),
                          style: muted,
                        ),
                        const SizedBox(height: FreshSpacing.s1),
                        Text(formatQuantity(line.quantity), style: theme.textTheme.bodyMedium),
                        PriceTag(
                          paise: line.estimatedLineTotalPaise,
                          prefix: 'Estimated',
                          style: theme.textTheme.titleSmall,
                        ),
                        if (line.maxLineTotalPaise != line.estimatedLineTotalPaise)
                          Text(
                            'At most ${formatRupees(line.maxLineTotalPaise)} for up to ${formatQuantity(line.maxChargeableQuantity)}',
                            style: muted,
                          ),
                      ],
                    ),
                  ),
                  IconButton(
                    tooltip: 'Remove ${line.productName}',
                    icon: const Icon(Icons.delete_outline),
                    onPressed: _pending ? null : _remove,
                  ),
                ],
              ),
              if (line.availability != null && line.availability!.status != 'IN_STOCK') ...[
                const SizedBox(height: FreshSpacing.s2),
                InfoBanner(
                  tone: line.availability!.canAdd ? BannerTone.warning : BannerTone.danger,
                  text: line.availability!.message ?? humanizeCode(line.availability!.status),
                ),
              ],
              for (final issue in line.issues) ...[
                const SizedBox(height: FreshSpacing.s2),
                InfoBanner(
                  tone: BannerTone.danger,
                  text: issue.message.isEmpty ? humanizeCode(issue.code) : issue.message,
                ),
              ],
              const SizedBox(height: FreshSpacing.s1),
              Align(
                alignment: Alignment.centerRight,
                child: _pending
                    ? const Padding(
                        padding: EdgeInsets.all(FreshSpacing.s3),
                        child: SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2, semanticsLabel: 'Updating cart'),
                        ),
                      )
                    : TextButton.icon(
                        onPressed: _changeQuantity,
                        icon: const Icon(Icons.edit_outlined),
                        label: Text('Change quantity of ${line.productName}', overflow: TextOverflow.ellipsis),
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Loads the product's server limits (min / max / step / maxOrderableNow) for the line's variant and
/// preparation, then lets the customer pick a quantity with the stepper. The cart line itself does not carry
/// these limits yet (see docs/design/mobile.md, API gaps).
class _QuantitySheet extends ConsumerStatefulWidget {
  const _QuantitySheet({required this.line});

  final CartLine line;

  @override
  ConsumerState<_QuantitySheet> createState() => _QuantitySheetState();
}

class _QuantitySheetState extends ConsumerState<_QuantitySheet> {
  late Quantity _value = widget.line.quantity;

  @override
  Widget build(BuildContext context) {
    final line = widget.line;
    final request = ProductRequest(line.productSlug, line.preparation?.code);
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(FreshSpacing.s4, 0, FreshSpacing.s4, FreshSpacing.s4),
        child: AsyncValueView<ProductDetail>(
          value: ref.watch(productDetailProvider(request)),
          onRetry: () => ref.invalidate(productDetailProvider(request)),
          loading: const LoadingSemantics(child: SkeletonBox(height: 120)),
          data: (detail) {
            final variant = detail.variants.where((v) => v.id == line.variantId).firstOrNull;
            if (variant == null) {
              return const EmptyState(slot: IllustrationSlot.outOfStock, title: 'This item is no longer sold');
            }
            return Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(line.productName, style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: FreshSpacing.s3),
                QuantityStepper(
                  value: _value,
                  min: variant.minOrder,
                  max: variant.availability?.maxOrderableNow ?? variant.maxOrder,
                  step: variant.orderStep,
                  itemName: line.productName,
                  onChanged: (q) => setState(() => _value = q ?? _value),
                ),
                const SizedBox(height: FreshSpacing.s4),
                PrimaryButton(label: 'Update', onPressed: () => Navigator.pop(context, _value)),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _CouponBox extends ConsumerStatefulWidget {
  const _CouponBox();

  @override
  ConsumerState<_CouponBox> createState() => _CouponBoxState();
}

class _CouponBoxState extends ConsumerState<_CouponBox> {
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
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    final coupon = ref.watch(cartProvider).value?.coupon;
    if (!signedIn) {
      return TextButton.icon(
        onPressed: () => context.push(Uri(path: '/login', queryParameters: {'from': '/cart'}).toString()),
        icon: const Icon(Icons.local_offer_outlined),
        label: const Text('Sign in to apply a coupon'),
      );
    }
    if (coupon != null) {
      final applied = coupon.status == 'APPLIED';
      return Row(
        children: [
          Expanded(
            child: InfoBanner(
              tone: applied ? BannerTone.success : BannerTone.warning,
              title: coupon.code,
              text:
                  coupon.message ??
                  (applied ? 'Applied: you save ${formatRupees(coupon.discountPaise)}.' : humanizeCode(coupon.status)),
            ),
          ),
          IconButton(
            tooltip: 'Remove coupon ${coupon.code}',
            onPressed: _busy ? null : _remove,
            icon: const Icon(Icons.close),
          ),
        ],
      );
    }
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: TextField(
            controller: _field,
            textCapitalization: TextCapitalization.characters,
            inputFormatters: [FilteringTextInputFormatter.allow(RegExp('[A-Za-z0-9_-]'))],
            decoration: InputDecoration(labelText: 'Coupon code', errorText: _error),
            onSubmitted: (_) => _apply(),
          ),
        ),
        const SizedBox(width: FreshSpacing.s2),
        SizedBox(
          height: 56,
          child: OutlinedButton(onPressed: _busy ? null : _apply, child: const Text('Apply')),
        ),
      ],
    );
  }
}
