// Reading again (D313, gap audit MB-20, MO-12, MO-15): after a while in the background, and by pulling the empty
// orders block.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/common.dart';
import 'package:fresh_orbit_app/api/models/orders.dart';
import 'package:fresh_orbit_app/features/orders/orders_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_orbit_app/state/cart_state.dart';
import 'package:fresh_orbit_app/widgets/refresh_on_resume.dart';

import '../support/harness.dart';

class _CartProbe extends ConsumerWidget {
  const _CartProbe();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(cartProvider);
    return const SizedBox();
  }
}

RefreshOnResumeState _state(WidgetTester tester) => tester.state<RefreshOnResumeState>(find.byType(RefreshOnResume));

Future<void> _background(WidgetTester tester) async {
  _state(tester).appHidden();
  await tester.pump();
}

Future<void> _foreground(WidgetTester tester) async {
  _state(tester).appShown();
  await tester.pumpAndSettle();
}

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  group('back from the background', () {
    late DateTime now;
    late TestDeps deps;

    Future<void> pump(WidgetTester tester) async {
      now = DateTime.utc(2026, 10, 29, 9);
      deps = TestDeps(pincode: '600041');
      await pumpScreen(tester, deps, RefreshOnResume(clock: () => now, child: const _CartProbe()));
      await tester.pumpAndSettle();
    }

    int cartReads() => deps.cart.calls.where((c) => c == 'get').length;

    testWidgets('after more than five minutes the cart (and the other reads) are read again', (tester) async {
      await pump(tester);
      expect(cartReads(), 1);

      await _background(tester);
      now = now.add(const Duration(minutes: 30));
      await _foreground(tester);

      expect(cartReads(), 2);
    });

    testWidgets('a short look at another app reads nothing again', (tester) async {
      await pump(tester);

      await _background(tester);
      now = now.add(const Duration(seconds: 40));
      await _foreground(tester);

      expect(cartReads(), 1);
    });
  });

  testWidgets('the empty orders block can be pulled to read again (MO-15)', (tester) async {
    final deps = TestDeps(signedIn: true);
    var reads = 0;
    deps.orders.onOrders = (_) async {
      reads++;
      return const Paged<OrderSummary>(items: [], page: 1, pageSize: 20, totalCount: 0);
    };
    await pumpScreen(tester, deps, const OrdersScreen(), otherPaths: ['/shop']);
    await tester.pumpAndSettle();
    expect(find.text('No orders yet'), findsOneWidget);

    await tester.widget<RefreshIndicator>(find.byKey(const Key('ordersEmptyRefresh'))).onRefresh();
    await tester.pumpAndSettle();

    expect(reads, 2);
  });
}
