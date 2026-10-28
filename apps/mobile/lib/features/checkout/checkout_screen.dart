// Checkout (D88): address → slot → payment → review (server preview with blockers) → place order, each step in
// a paper box, slots as buttons, and the sticky pay bar with the server's expected total.
// The place-order button sends one Idempotency-Key per tap-and-retries; PRICE_CHANGED opens a dialog with the
// server's old and new totals. See state/checkout_state.dart for the rules.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/account.dart';
import '../../api/models/checkout.dart';
import '../../api/models/locations.dart';
import '../../core/format.dart';
import '../../state/account_state.dart';
import '../../state/checkout_state.dart';
import '../../state/location_state.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';
import '../cart/totals_card.dart';

const _fallbackMethods = ['COD', 'UPI_ON_DELIVERY'];

class CheckoutScreen extends ConsumerStatefulWidget {
  const CheckoutScreen({super.key});

  @override
  ConsumerState<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends ConsumerState<CheckoutScreen> {
  bool _autoSelected = false;
  bool _dialogOpen = false;

  CheckoutController get _checkout => ref.read(checkoutProvider.notifier);

  void _autoSelectDefault(List<Address> addresses) {
    if (_autoSelected || addresses.isEmpty || ref.read(checkoutProvider).addressId != null) return;
    _autoSelected = true;
    final preferred = addresses.where((a) => a.isDefault).firstOrNull ?? addresses.first;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _checkout.selectAddress(preferred.id, pincode: preferred.pincode);
    });
  }

  Future<void> _addAddress() async {
    final created = await context.push<Address>('/addresses/new');
    if (created != null && mounted) await _checkout.selectAddress(created.id, pincode: created.pincode);
  }

  /// Edits an address in place (the form starts filled with it); the chosen one is priced again.
  Future<void> _editAddress(Address a) async {
    final saved = await context.push<Address>('/addresses/${Uri.encodeComponent(a.id)}');
    if (saved != null && mounted) await _checkout.addressEdited(saved.id, pincode: saved.pincode);
  }

  Future<void> _place() async {
    final order = await _checkout.placeOrder();
    if (order != null && mounted) context.go('/order-success/${order.id}');
  }

  Future<void> _showPriceChanged(CheckoutPreview? before, CheckoutPreview after) async {
    if (_dialogOpen) return;
    _dialogOpen = true;
    final accept = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (context) => _PriceChangedDialog(before: before, after: after),
    );
    _dialogOpen = false;
    if (!mounted) return;
    if (accept == true) {
      final order = await _checkout.acceptNewPrice();
      if (order != null && mounted) context.go('/order-success/${order.id}');
    } else {
      _checkout.declineNewPrice();
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(checkoutProvider, (prev, next) {
      final changed = next.priceChanged;
      if (changed != null && prev?.priceChanged == null) {
        _showPriceChanged(prev?.preview?.value, changed);
      }
    });
    final state = ref.watch(checkoutProvider);
    final addresses = ref.watch(addressesProvider);
    final theme = Theme.of(context);
    final preview = state.preview;

    return Scaffold(
      appBar: const PageHeader(text: 'Almost', brushed: 'there'),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, FreshLayout.sectionGap),
        children: [
          _Step(
            number: 1,
            title: 'Delivery address',
            child: AsyncValueView<List<Address>>(
              value: addresses,
              onRetry: () => ref.invalidate(addressesProvider),
              loading: const LoadingSemantics(child: SkeletonBox(height: 72)),
              data: (list) {
                _autoSelectDefault(list);
                return Column(
                  children: [
                    RadioGroup<String>(
                      groupValue: state.addressId,
                      onChanged: (id) {
                        final a = list.firstWhere((x) => x.id == id);
                        _checkout.selectAddress(a.id, pincode: a.pincode);
                      },
                      child: Column(
                        children: [
                          for (final a in list)
                            RadioListTile<String>(
                              key: ValueKey('checkoutAddress-${a.id}'),
                              contentPadding: EdgeInsets.zero,
                              value: a.id,
                              title: Text('${humanizeCode(a.label)} · ${a.recipientName}'),
                              subtitle: Text(a.oneLine),
                              secondary: IconButton(
                                key: ValueKey('editAddress-${a.id}'),
                                tooltip: 'Edit ${humanizeCode(a.label)} address',
                                icon: const Icon(Icons.edit_outlined),
                                onPressed: () => _editAddress(a),
                              ),
                            ),
                        ],
                      ),
                    ),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton.icon(
                        key: const Key('addAddress'),
                        onPressed: _addAddress,
                        icon: const Icon(Icons.add),
                        label: const Text('Add a new address'),
                      ),
                    ),
                  ],
                );
              },
            ),
          ),
          _Step(
            number: 2,
            title: 'Delivery slot',
            child: state.addressPincode == null
                ? Text('Choose an address to see delivery slots.', style: theme.textTheme.bodyMedium)
                : _SlotPicker(
                    pincode: state.addressPincode!,
                    selected: state.slotId,
                    preferred: ref.watch(preferredSlotProvider),
                    onSelected: _checkout.selectSlot,
                  ),
          ),
          _Step(
            number: 3,
            title: 'Payment',
            child: _PaymentPicker(
              options: preview?.value?.paymentMethods,
              selected: state.paymentMethod,
              onSelected: _checkout.selectPayment,
            ),
          ),
          _Step(
            number: 4,
            title: 'If an item is short',
            child: Column(
              children: [
                RadioGroup<String>(
                  groupValue: state.substitutionPreference,
                  onChanged: (v) => _checkout.setSubstitutionPreference(v!),
                  child: const Column(
                    children: [
                      RadioListTile<String>(
                        contentPadding: EdgeInsets.zero,
                        value: 'CALL_ME',
                        title: Text('Call me before replacing it'),
                      ),
                      RadioListTile<String>(
                        contentPadding: EdgeInsets.zero,
                        value: 'AUTO_SIMILAR',
                        title: Text('Replace with a similar item'),
                        subtitle: Text('Only when it costs the same or less.'),
                      ),
                      RadioListTile<String>(
                        key: Key('substitutionNone'),
                        contentPadding: EdgeInsets.zero,
                        value: 'NONE',
                        title: Text('Leave it out'),
                        subtitle: Text('We remove it from your order.'),
                      ),
                    ],
                  ),
                ),
                TextField(
                  maxLength: 500,
                  minLines: 1,
                  maxLines: 3,
                  decoration: const InputDecoration(labelText: 'Note for the delivery partner (optional)'),
                  onChanged: _checkout.setNotes,
                ),
              ],
            ),
          ),
          _Step(
            number: 5,
            title: 'Review',
            child: preview == null
                ? Text('Choose an address and a slot to see your bill.', style: theme.textTheme.bodyMedium)
                : AsyncValueView<CheckoutPreview>(
                    value: preview,
                    onRetry: _checkout.refreshPreview,
                    loading: const LoadingSemantics(label: 'Pricing your order', child: SkeletonBox(height: 220)),
                    data: (p) => _Review(preview: p),
                  ),
          ),
          if (state.placeError != null) ...[
            const SizedBox(height: FreshSpacing.s3),
            InfoBanner(
              key: const Key('placeError'),
              tone: BannerTone.danger,
              title: state.placeError!.isTransient ? 'We could not confirm your order' : 'Order not placed',
              text: state.placeError!.isTransient
                  ? '${state.placeError!.message} Tap "Place order" again; you will not be charged twice.'
                  : state.placeError!.message,
            ),
          ],
        ],
      ),
      bottomNavigationBar: StickyActionBar(
        leading: _PayMethod(method: state.paymentMethod),
        action: TealActionButton(
          key: const Key('placeOrder'),
          label: 'Place order',
          amount: preview?.value == null ? null : formatRupees(preview!.value!.expectedTotalPaise),
          semanticLabel: preview?.value == null
              ? null
              : 'Place order, ${formatRupees(preview!.value!.expectedTotalPaise)}',
          loading: state.placing,
          onPressed: state.canPlace ? _place : null,
        ),
      ),
    );
  }
}

/// One checkout step in a paper box with a numbered title.
class _Step extends StatelessWidget {
  const _Step({required this.number, required this.title, required this.child});

  final int number;
  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: FreshLayout.cardGap),
      child: SectionBox(
        padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 14, FreshLayout.gutter, FreshLayout.gutter),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(
              header: true,
              label: 'Step $number, $title',
              excludeSemantics: true,
              child: Row(
                children: [
                  Container(
                    width: 26,
                    height: 26,
                    alignment: Alignment.center,
                    decoration: const BoxDecoration(color: FreshHighlightColors.brush, shape: BoxShape.circle),
                    child: Text('$number', style: AppText.display(FreshHighlightColors.ink, 14, height: 1)),
                  ),
                  const SizedBox(width: 10),
                  Expanded(child: Text(title, style: AppText.display(c.ink, 17))),
                ],
              ),
            ),
            const SizedBox(height: 8),
            child,
          ],
        ),
      ),
    );
  }
}

/// The left part of the pay bar: the chosen way to pay.
class _PayMethod extends StatelessWidget {
  const _PayMethod({required this.method});

  final String method;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('PAYING BY', style: AppText.eyebrow(c.muted, size: 10)),
        Text(
          paymentMethodLabel(method),
          style: TextStyle(fontFamily: AppFonts.body, fontSize: 13, fontWeight: FontWeight.w800, color: c.ink),
        ),
      ],
    );
  }
}

class _SlotPicker extends ConsumerStatefulWidget {
  const _SlotPicker({required this.pincode, required this.selected, required this.onSelected, this.preferred});

  final String pincode;
  final String? selected;

  /// The slot tapped in the cart; chosen here once when this address offers it and it is open.
  final String? preferred;
  final ValueChanged<String> onSelected;

  @override
  ConsumerState<_SlotPicker> createState() => _SlotPickerState();
}

class _SlotPickerState extends ConsumerState<_SlotPicker> {
  String? _day;
  bool _preferredTried = false;

  void _choosePreferred(DeliverySlots slots) {
    if (_preferredTried || widget.selected != null || widget.preferred == null) return;
    _preferredTried = true;
    final slot = slots.items.where((s) => s.id == widget.preferred && s.isBookable).firstOrNull;
    if (slot == null) return;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) widget.onSelected(slot.id);
    });
  }

  static String _statusText(String status) => switch (status) {
    'FULL' => 'Full',
    'CUTOFF_PASSED' => 'Booking closed',
    'CLOSED' => 'Closed',
    _ => humanizeCode(status),
  };

  @override
  Widget build(BuildContext context) {
    final provider = deliverySlotsProvider(widget.pincode);
    return AsyncValueView<DeliverySlots>(
      value: ref.watch(provider),
      onRetry: () => ref.invalidate(provider),
      loading: const LoadingSemantics(child: SkeletonBox(height: 96)),
      data: (slots) {
        if (slots.items.isEmpty) {
          return const InfoBanner(
            tone: BannerTone.warning,
            text: 'No delivery slots are open for this address right now.',
          );
        }
        _choosePreferred(slots);
        final days = <String>[];
        for (final s in slots.items) {
          if (!days.contains(s.slotDate)) days.add(s.slotDate);
        }
        final selectedSlot = slots.items.where((s) => s.id == widget.selected).firstOrNull;
        final day = _day ?? selectedSlot?.slotDate ?? days.first;
        final daySlots = slots.items.where((s) => s.slotDate == day).toList();
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: FreshSpacing.s2,
              children: [
                for (final d in days)
                  ChoiceChip(
                    label: Text(formatCalendarDate(d)),
                    selected: d == day,
                    onSelected: (_) => setState(() => _day = d),
                  ),
              ],
            ),
            const SizedBox(height: FreshSpacing.s2),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final s in daySlots)
                  SlotButton(
                    key: ValueKey('slot-${s.id}'),
                    title: formatTimeRange(s.startAt, s.endAt),
                    subtitle: s.isBookable ? 'Order by ${formatTime(s.cutoffAt)}' : _statusText(s.status),
                    selected: s.id == widget.selected,
                    onTap: s.isBookable ? () => widget.onSelected(s.id) : null,
                  ),
              ],
            ),
          ],
        );
      },
    );
  }
}

class _PaymentPicker extends StatelessWidget {
  const _PaymentPicker({required this.options, required this.selected, required this.onSelected});

  final List<PaymentMethodOption>? options;
  final String selected;
  final ValueChanged<String> onSelected;

  @override
  Widget build(BuildContext context) {
    final list = options ?? [for (final m in _fallbackMethods) PaymentMethodOption(method: m, enabled: true)];
    return RadioGroup<String>(
      groupValue: selected,
      onChanged: (m) => onSelected(m!),
      child: Column(
        children: [
          for (final o in list)
            RadioListTile<String>(
              contentPadding: EdgeInsets.zero,
              value: o.method,
              enabled: o.enabled,
              title: Text(paymentMethodLabel(o.method)),
              subtitle: o.enabled ? null : const Text('Not available yet'),
            ),
        ],
      ),
    );
  }
}

class _Review extends StatelessWidget {
  const _Review({required this.preview});

  final CheckoutPreview preview;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final b in preview.blockers)
          Padding(
            padding: const EdgeInsets.only(bottom: FreshSpacing.s2),
            child: InfoBanner(tone: BannerTone.danger, text: b.message.isEmpty ? humanizeCode(b.code) : b.message),
          ),
        if (preview.slot != null)
          Text(
            'Delivery ${formatSlotWindow(preview.slot!.startAt, preview.slot!.endAt)}',
            style: theme.textTheme.bodyMedium,
          ),
        const SizedBox(height: FreshSpacing.s2),
        for (final l in preview.lines)
          ListTile(
            contentPadding: EdgeInsets.zero,
            dense: true,
            title: Text(l.productName),
            subtitle: Text('${formatQuantity(l.quantity)}${l.preparation == null ? '' : ' · ${l.preparation!.name}'}'),
            trailing: Text(formatRupees(l.estimatedLineTotalPaise)),
          ),
        if (preview.coupon != null)
          Padding(
            padding: const EdgeInsets.only(bottom: FreshSpacing.s2),
            child: InfoBanner(
              tone: preview.coupon!.status == 'APPLIED' ? BannerTone.success : BannerTone.warning,
              title: preview.coupon!.code,
              text: preview.coupon!.message ?? humanizeCode(preview.coupon!.status),
            ),
          ),
        TotalsCard(totals: preview.totals, title: 'Bill'),
      ],
    );
  }
}

/// PRICE_CHANGED: old and new totals as sent by the server; the customer accepts or goes back.
class _PriceChangedDialog extends StatelessWidget {
  const _PriceChangedDialog({required this.before, required this.after});

  final CheckoutPreview? before;
  final CheckoutPreview after;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return AlertDialog(
      title: const Text('Prices have changed'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Some prices or charges changed since you reviewed your order. Please check the new total.'),
            const SizedBox(height: FreshSpacing.s3),
            if (before != null)
              Text(
                'Before: ${formatRupees(before!.totals.estimatedTotalPaise)}',
                key: const Key('oldTotal'),
                style: theme.textTheme.bodyMedium?.copyWith(decoration: TextDecoration.lineThrough),
              ),
            Text(
              'Now: ${formatRupees(after.totals.estimatedTotalPaise)}',
              key: const Key('newTotal'),
              style: theme.textTheme.titleMedium,
            ),
            Text('At most ${formatRupees(after.totals.maxTotalPaise)}', style: theme.textTheme.bodySmall),
            for (final b in after.blockers) ...[
              const SizedBox(height: FreshSpacing.s2),
              InfoBanner(tone: BannerTone.danger, text: b.message),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Go back')),
        FilledButton(
          onPressed: after.canPlaceOrder ? () => Navigator.pop(context, true) : null,
          child: const Text('Accept new price'),
        ),
      ],
    );
  }
}
