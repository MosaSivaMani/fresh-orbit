// Reading again when the app comes back (D313, gap audit MB-20, MO-12, MO-15). A customer who leaves the app for
// a while and comes back sees the server's current prices, stock, offers, cart, delivery slots and orders, not
// the ones from hours ago. Only after [staleAfter] away: a quick look at an SMS code does not reload everything.

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../state/cart_state.dart';
import '../state/catalog_state.dart';
import '../state/location_state.dart';
import '../state/orders_state.dart';

/// How long the app may be in the background before the screens read again.
const refreshAfterBackground = Duration(minutes: 5);

/// Invalidates what the server may have changed while the app was away.
void refreshServerReads(WidgetRef ref) {
  ref.invalidate(currentServiceabilityProvider);
  ref.invalidate(homeProvider);
  ref.invalidate(productListProvider);
  ref.invalidate(cartProvider);
  ref.invalidate(deliverySlotsProvider);
  ref.invalidate(ordersProvider);
}

/// Wraps the tab shell: on resume after [staleAfter] in the background, the home, the lists, the cart, the slots
/// and the orders are read again.
class RefreshOnResume extends ConsumerStatefulWidget {
  const RefreshOnResume({super.key, required this.child, this.staleAfter = refreshAfterBackground, this.clock});

  final Widget child;
  final Duration staleAfter;

  /// For tests; the device clock otherwise.
  final DateTime Function()? clock;

  @override
  ConsumerState<RefreshOnResume> createState() => RefreshOnResumeState();
}

class RefreshOnResumeState extends ConsumerState<RefreshOnResume> {
  late final AppLifecycleListener _listener;
  DateTime? _leftAt;

  DateTime _now() => (widget.clock ?? DateTime.now)();

  @override
  void initState() {
    super.initState();
    _listener = AppLifecycleListener(onHide: appHidden, onShow: appShown);
  }

  /// The app went to the background (public for tests).
  void appHidden() => _leftAt ??= _now();

  /// The app is visible again: read the server again when it was away for [RefreshOnResume.staleAfter] or more.
  void appShown() {
    final left = _leftAt;
    _leftAt = null;
    if (left != null && _now().difference(left) >= widget.staleAfter && mounted) refreshServerReads(ref);
  }

  @override
  void dispose() {
    _listener.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
