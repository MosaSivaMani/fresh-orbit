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
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/heading.dart';
import '../../widgets/illustrated.dart';
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

  void _tryAnother() {
    _field.clear();
    setState(() => _query = null);
    _focus.requestFocus();
  }

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final query = _query;
    final result = query == null ? null : ref.watch(serviceabilityProvider(query));
    final back = widget.changing && context.canPop() ? () => context.pop() : null;

    final checked = result?.value;
    if (checked != null && !checked.serviceable && !result!.isLoading) {
      return Scaffold(
        backgroundColor: BlockTone.ice.background(c),
        body: Semantics(
          liveRegion: true,
          child: ColorBlockScreen(
            tone: BlockTone.ice,
            eyebrow: checked.pincode,
            heading: const [HeadingLine('Not here'), HeadingLine('yet', script: true, brush: true)],
            slot: IllustrationSlot.notServiceable,
            onBack: back,
            sticker: const StickerSpot(
              right: 30,
              top: 30,
              child: Sticker(label: 'Coming soon', tone: StickerTone.mango, tilt: 0.017),
            ),
            children: [
              SheetTitle('We don’t deliver to ${checked.pincode} yet', header: true),
              const SheetNote('We are adding new areas. Try a nearby pincode, such as your office.'),
              SizedBox(
                width: double.infinity,
                child: FilledButton(onPressed: _tryAnother, child: const Text('Try another pincode')),
              ),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: BlockTone.ice.background(c),
      body: ColorBlockScreen(
        tone: BlockTone.ice,
        eyebrow: 'Delivery area',
        heading: const [HeadingLine('Where do we'), HeadingLine('deliver?', script: true)],
        slot: IllustrationSlot.locationPick,
        avatarSize: AvatarSize.small,
        onBack: back,
        sheetCrossAxis: CrossAxisAlignment.stretch,
        children: [
          const SheetNote('Prices, stock and delivery slots depend on your nearest store.'),
          TextField(
            key: const Key('pincodeField'),
            controller: _field,
            focusNode: _focus,
            keyboardType: TextInputType.number,
            textInputAction: TextInputAction.search,
            autofillHints: const [AutofillHints.postalCode],
            inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(6)],
            decoration: InputDecoration(labelText: 'Pincode', hintText: 'e.g. 600041', errorText: _inputError),
            onSubmitted: (_) => _check(),
          ),
          FilledButton(key: const Key('checkPincode'), onPressed: _check, child: const Text('Check my area')),
          if (query != null && result != null)
            result.when(
              loading: () =>
                  const LoadingSemantics(label: 'Checking pincode', child: SkeletonBox(height: 160, radius: 18)),
              error: (e, _) =>
                  ErrorState(error: e, compact: true, onRetry: () => ref.invalidate(serviceabilityProvider(query))),
              data: (s) => _ServiceableCard(s: s, saving: _saving, onConfirm: () => _confirm(s)),
            ),
        ],
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
    return SectionBox(
      color: illustratedColors(context).sand,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          InfoBanner(tone: BannerTone.success, text: 'We deliver to ${s.pincode}.'),
          const SizedBox(height: FreshSpacing.s3),
          if (s.cityName != null) Text(s.cityName!, style: theme.textTheme.titleMedium),
          if (s.locationName != null) Text('From ${s.locationName}', style: theme.textTheme.bodyMedium),
          const SizedBox(height: FreshSpacing.s2),
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
            child: TealActionButton(
              key: const Key('confirmPincode'),
              label: saving ? 'Saving…' : 'Deliver here',
              loading: saving,
              onPressed: onConfirm,
            ),
          ),
        ],
      ),
    );
  }
}
