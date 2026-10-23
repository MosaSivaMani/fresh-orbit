import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/features/onboarding/location_screen.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

void main() {
  testWidgets('rejects a malformed pincode without calling the server', (tester) async {
    final deps = TestDeps();
    await pumpScreen(tester, deps, const LocationScreen());

    await tester.enterText(find.byKey(const Key('pincodeField')), '1234');
    await tester.tap(find.text('Check'));
    await tester.pump();

    expect(find.text('Enter a 6-digit pincode.'), findsOneWidget);
    expect(deps.location.checked, isEmpty);
  });

  testWidgets('serviceable pincode shows server fees and next slot, then stores the pincode', (tester) async {
    final deps = TestDeps();
    deps.location.onServiceability = (_) async => serviceable();
    await pumpScreen(tester, deps, const LocationScreen());

    await tester.enterText(find.byKey(const Key('pincodeField')), '600041');
    await tester.tap(find.text('Check'));
    await tester.pumpAndSettle();

    expect(find.text('We deliver to 600041.'), findsOneWidget);
    expect(find.text('Delivery fee ₹39'), findsOneWidget);
    expect(find.text('Free delivery above ₹499'), findsOneWidget);
    expect(find.text('Minimum order ₹299'), findsOneWidget);
    expect(find.textContaining('2:00 pm – 4:00 pm'), findsOneWidget);

    await tester.tap(find.byKey(const Key('confirmPincode')));
    await tester.pumpAndSettle();

    expect(deps.prefs.pincode, '600041');
    expect(find.text('route:/home'), findsOneWidget);
  });

  testWidgets('unserviceable pincode shows the unserviceable state and lets the customer try again', (tester) async {
    final deps = TestDeps();
    deps.location.onServiceability = (p) async => notServiceable(p);
    await pumpScreen(tester, deps, const LocationScreen());

    await tester.enterText(find.byKey(const Key('pincodeField')), '600123');
    await tester.tap(find.text('Check'));
    await tester.pumpAndSettle();

    expect(find.text('We don\'t deliver to 600123 yet'), findsOneWidget);
    expect(find.byKey(const Key('confirmPincode')), findsNothing);
    expect(deps.prefs.pincode, isNull);

    await tester.tap(find.text('Try another pincode'));
    await tester.pump();
    expect(find.text('We don\'t deliver to 600123 yet'), findsNothing);
  });

  testWidgets('a failed check shows the error with its reference and a retry', (tester) async {
    final deps = TestDeps();
    var calls = 0;
    deps.location.onServiceability = (_) async {
      calls++;
      if (calls == 1) throw const ApiError(status: 503, code: 'SERVICE_UNAVAILABLE', traceId: 'trace-1');
      return serviceable();
    };
    await pumpScreen(tester, deps, const LocationScreen());

    await tester.enterText(find.byKey(const Key('pincodeField')), '600041');
    await tester.tap(find.text('Check'));
    await tester.pumpAndSettle();

    expect(find.text('Reference: trace-1'), findsOneWidget);
    await tester.tap(find.text('Try again'));
    await tester.pumpAndSettle();
    expect(find.text('We deliver to 600041.'), findsOneWidget);
  });
}
