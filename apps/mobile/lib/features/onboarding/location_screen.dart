// Pincode entry → server serviceability (§4.3). Serviceable: show city, store, fees and next slot, then store
// the pincode (and move the cart, confirming CART_LOCATION_CHANGED). Unserviceable: a clear state with
// "try another pincode". The app never decides serviceability itself.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/locations.dart';
import '../../core/format.dart';
import '../../state/cart_state.dart';
import '../../state/location_state.dart';
import '../../widgets/states.dart';

class LocationScreen extends ConsumerStatefulWidget {
  const LocationScreen({super.key, this.changing = false});

  /// Opened from the pincode chip (back returns to the previous screen).
  final bool changing;

  @override
  ConsumerState<LocationScreen> createState() => _LocationScreenState();
}

class _LocationScreenState extends ConsumerState<LocationScreen> {
  final _field = TextEditingController();
  final _focus = FocusNode();
  String? _query;
  String? _inputError;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _field.text = ref.read(pincodeProvider) ?? '';
  }

  @override
  void dispose() {
    _field.dispose();
    _focus.dispose();
    super.dispose();
  }

  void _check() {
    final value = _field.text.trim();
    if (!isPincodeShape(value)) {
      setState(() => _inputError = 'Enter a 6-digit pincode.');
      return;
    }
    FocusScope.of(context).unfocus();
    setState(() {
      _inputError = null;
      _query = value;
    });
  }

  Future<void> _confirm(Serviceability s) async {
    setState(() => _saving = true);
    try {
      final cart = ref.read(cartProvider).value;
      if (cart != null && !cart.isEmpty && cart.pincode != null && cart.pincode != s.pincode) {
        final moved = await _moveCart(s.pincode);
        if (!moved) return;
      }
      await ref.read(pincodeProvider.notifier).set(s.pincode);
      if (!mounted) return;
      if (widget.changing && context.canPop()) {
        context.pop();
      } else {
        context.go('/home');
      }
    } catch (e) {
      if (mounted) showErrorSnack(context, e);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  /// PUT /cart/location; on 409 CART_LOCATION_CHANGED ask the customer, then force.
  Future<bool> _moveCart(String pincode) async {
    final cart = ref.read(cartProvider.notifier);
    try {
      await cart.changeLocation(pincode);
      return true;
    } on ApiError catch (e) {
      if (e.code != 'CART_LOCATION_CHANGED' || !mounted) rethrow;
      final ok = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Some cart items will be removed'),
          content: Text(e.detail ?? 'A few items in your cart are not sold at the store that delivers to $pincode.'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Keep old pincode')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Change pincode')),
          ],
        ),
      );
      if (ok != true) return false;
      await cart.changeLocation(pincode, force: true);
      return true;
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final query = _query;
    return Scaffold(
      appBar: AppBar(title: const Text('Delivery location'), automaticallyImplyLeading: widget.changing),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(FreshSpacing.s4),
          children: [
            Semantics(header: true, child: Text('Where should we deliver?', style: theme.textTheme.headlineSmall)),
            const SizedBox(height: FreshSpacing.s2),
            Text('Prices, stock and delivery slots depend on your area.', style: theme.textTheme.bodyMedium),
            const SizedBox(height: FreshSpacing.s5),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: TextField(
                    key: const Key('pincodeField'),
                    controller: _field,
                    focusNode: _focus,
                    keyboardType: TextInputType.number,
                    textInputAction: TextInputAction.search,
                    autofillHints: const [AutofillHints.postalCode],
                    inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(6)],
                    decoration: InputDecoration(labelText: 'Pincode', errorText: _inputError),
                    onSubmitted: (_) => _check(),
                  ),
                ),
                const SizedBox(width: FreshSpacing.s3),
                SizedBox(
                  height: 56,
                  child: FilledButton(onPressed: _check, child: const Text('Check')),
                ),
              ],
            ),
            const SizedBox(height: FreshSpacing.s5),
            if (query != null)
              ref
                  .watch(serviceabilityProvider(query))
                  .when(
                    loading: () => const LoadingSemantics(
                      label: 'Checking pincode',
                      child: SkeletonBox(height: 160, radius: FreshRadius.lg),
                    ),
                    error: (e, _) => ErrorState(
                      error: e,
                      compact: true,
                      onRetry: () => ref.invalidate(serviceabilityProvider(query)),
                    ),
                    data: (s) => s.serviceable
                        ? _ServiceableCard(s: s, saving: _saving, onConfirm: () => _confirm(s))
                        : _NotServiceable(
                            pincode: s.pincode,
                            onTryAnother: () {
                              _field.clear();
                              setState(() => _query = null);
                              _focus.requestFocus();
                            },
                          ),
                  ),
          ],
        ),
      ),
    );
  }
}

class _ServiceableCard extends StatelessWidget {
  const _ServiceableCard({required this.s, required this.saving, required this.onConfirm});

  final Serviceability s;
  final bool saving;
  final VoidCallback onConfirm;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final slot = s.nextSlot;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(FreshSpacing.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            InfoBanner(tone: BannerTone.success, text: 'We deliver to ${s.pincode}.'),
            const SizedBox(height: FreshSpacing.s3),
            if (s.cityName != null) Text(s.cityName!, style: theme.textTheme.titleMedium),
            if (s.locationName != null) Text('From ${s.locationName}', style: theme.textTheme.bodyMedium),
            const SizedBox(height: FreshSpacing.s3),
            if (s.deliveryFeePaise != null)
              Text(
                s.deliveryFeePaise == 0 ? 'Free delivery' : 'Delivery fee ${formatRupees(s.deliveryFeePaise!)}',
                style: theme.textTheme.bodyMedium,
              ),
            if (s.freeDeliveryAbovePaise != null)
              Text('Free delivery above ${formatRupees(s.freeDeliveryAbovePaise!)}', style: theme.textTheme.bodyMedium),
            if (s.minOrderPaise != null && s.minOrderPaise! > 0)
              Text('Minimum order ${formatRupees(s.minOrderPaise!)}', style: theme.textTheme.bodyMedium),
            if (slot != null)
              Text('Next delivery: ${formatSlotWindow(slot.startAt, slot.endAt)}', style: theme.textTheme.bodyMedium),
            const SizedBox(height: FreshSpacing.s4),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                key: const Key('confirmPincode'),
                onPressed: saving ? null : onConfirm,
                child: Text(saving ? 'Saving…' : 'Deliver here'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _NotServiceable extends StatelessWidget {
  const _NotServiceable({required this.pincode, required this.onTryAnother});

  final String pincode;
  final VoidCallback onTryAnother;

  @override
  Widget build(BuildContext context) => Semantics(
    liveRegion: true,
    child: EmptyState(
      icon: Icons.location_off_outlined,
      title: 'We don\'t deliver to $pincode yet',
      message: 'We are adding new areas. Try a nearby pincode, such as your office.',
      action: OutlinedButton(onPressed: onTryAnother, child: const Text('Try another pincode')),
    ),
  );
}
