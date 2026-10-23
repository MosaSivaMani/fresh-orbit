// Checkout: address → slot → payment → review (server preview with blockers) → place order.
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
    final created = await context.push<Address>('/account/addresses/new');
    if (created != null && mounted) await _checkout.selectAddress(created.id, pincode: created.pincode);
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
      appBar: AppBar(title: const Text('Checkout')),
      body: ListView(
        padding: const EdgeInsets.all(FreshSpacing.s4),
        children: [
          const _StepTitle(number: 1, title: 'Delivery address'),
          AsyncValueView<List<Address>>(
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
                            contentPadding: EdgeInsets.zero,
                            value: a.id,
                            title: Text('${humanizeCode(a.label)} · ${a.recipientName}'),
                            subtitle: Text(a.oneLine),
                          ),
                      ],
                    ),
                  ),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton.icon(
                      onPressed: _addAddress,
                      icon: const Icon(Icons.add),
                      label: const Text('Add a new address'),
                    ),
                  ),
                ],
              );
            },
          ),
          const SizedBox(height: FreshSpacing.s4),
          const _StepTitle(number: 2, title: 'Delivery slot'),
          if (state.addressPincode == null)
            Text('Choose an address to see delivery slots.', style: theme.textTheme.bodyMedium)
          else
            _SlotPicker(
              pincode: state.addressPincode!,
              selected: state.slotId,
              onSelected: _checkout.selectSlot,
            ),
          const SizedBox(height: FreshSpacing.s4),
          const _StepTitle(number: 3, title: 'Payment'),
          _PaymentPicker(
            options: preview?.value?.paymentMethods,
            selected: state.paymentMethod,
            onSelected: _checkout.selectPayment,
          ),
          const SizedBox(height: FreshSpacing.s4),
          const _StepTitle(number: 4, title: 'If an item is short'),
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
          const SizedBox(height: FreshSpacing.s4),
          const _StepTitle(number: 5, title: 'Review'),
          if (preview == null)
            Text('Choose an address and a slot to see your bill.', style: theme.textTheme.bodyMedium)
          else
            AsyncValueView<CheckoutPreview>(
              value: preview,
              onRetry: _checkout.refreshPreview,
              loading: const LoadingSemantics(label: 'Pricing your order', child: SkeletonBox(height: 220)),
              data: (p) => _Review(preview: p),
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
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(FreshSpacing.s4),
          child: PrimaryButton(
            key: const Key('placeOrder'),
            label: preview?.value == null
                ? 'Place order'
                : 'Place order · ${formatRupees(preview!.value!.expectedTotalPaise)}',
            loading: state.placing,
            onPressed: state.canPlace ? _place : null,
          ),
        ),
      ),
    );
  }
}

class _StepTitle extends StatelessWidget {
  const _StepTitle({required this.number, required this.title});

  final int number;
  final String title;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: FreshSpacing.s2),
        child: Semantics(
          header: true,
          child: Text('$number. $title', style: Theme.of(context).textTheme.titleMedium),
        ),
      );
}

class _SlotPicker extends ConsumerStatefulWidget {
  const _SlotPicker({required this.pincode, required this.selected, required this.onSelected});

  final String pincode;
  final String? selected;
  final ValueChanged<String> onSelected;

  @override
  ConsumerState<_SlotPicker> createState() => _SlotPickerState();
}

class _SlotPickerState extends ConsumerState<_SlotPicker> {
  String? _day;

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
          return const InfoBanner(tone: BannerTone.warning, text: 'No delivery slots are open for this address right now.');
        }
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
                  ChoiceChip(label: Text(formatCalendarDate(d)), selected: d == day, onSelected: (_) => setState(() => _day = d)),
              ],
            ),
            RadioGroup<String>(
              groupValue: widget.selected,
              onChanged: (id) => widget.onSelected(id!),
              child: Column(
                children: [
                  for (final s in daySlots)
                    RadioListTile<String>(
                      contentPadding: EdgeInsets.zero,
                      value: s.id,
                      enabled: s.isBookable,
                      title: Text(formatTimeRange(s.startAt, s.endAt)),
                      subtitle: s.isBookable
                          ? Text('Order by ${formatTime(s.cutoffAt)}')
                          : Text(_statusText(s.status)),
                    ),
                ],
              ),
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
    final list = options ??
        [for (final m in _fallbackMethods) PaymentMethodOption(method: m, enabled: true)];
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
          Text('Delivery ${formatSlotWindow(preview.slot!.startAt, preview.slot!.endAt)}', style: theme.textTheme.bodyMedium),
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
