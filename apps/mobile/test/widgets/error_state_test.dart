import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/api_error.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_orbit_app/widgets/states.dart';

import '../support/harness.dart';

Future<void> _pump(WidgetTester tester, String code, {int status = 422}) async {
  await pumpScreen(
    tester,
    TestDeps(pincode: '110001'),
    Scaffold(
      body: ErrorState(
        error: ApiError(status: status, code: code, traceId: 't1'),
        onRetry: () {},
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  for (final code in ['PINCODE_NOT_SERVICEABLE', 'LOCATION_REQUIRED']) {
    testWidgets('$code offers Change pincode, which opens the pincode screen (D236)', (tester) async {
      await _pump(tester, code);

      expect(find.byKey(const Key('changePincode')), findsOneWidget);
      expect(find.text('Try again'), findsOneWidget);
      await tester.tap(find.byKey(const Key('changePincode')));
      await tester.pumpAndSettle();
      expect(find.text('route:/location?change=1'), findsOneWidget);
    });
  }

  testWidgets('other refusals keep only Try again', (tester) async {
    await _pump(tester, 'VALIDATION_FAILED');

    expect(find.byKey(const Key('changePincode')), findsNothing);
    expect(find.text('Try again'), findsOneWidget);
  });
}
