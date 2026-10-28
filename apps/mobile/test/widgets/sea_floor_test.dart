// The sea-floor footer (D289): a static, decorative band at the bottom of calm pages, and the screens that use it.
// Each screen must fit a small phone (360 × 640) without overflow, with the band at the bottom.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/features/account/help_screen.dart';
import 'package:fresh_orbit_app/features/cart/cart_screen.dart';
import 'package:fresh_orbit_app/features/onboarding/location_screen.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

Future<void> _pumpPart(
  WidgetTester tester,
  Widget body, {
  double width = 360,
  EdgeInsets padding = EdgeInsets.zero,
}) async {
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = Size(width * 3, 640 * 3);
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.light(),
      home: MediaQuery(
        data: MediaQueryData(size: Size(width, 640), padding: padding),
        child: Scaffold(body: body),
      ),
    ),
  );
}

/// Pumps [screen] at 360 × 640.
Future<void> _pumpSmall(WidgetTester tester, TestDeps deps, Widget screen) async {
  await pumpScreen(tester, deps, screen);
  tester.view.physicalSize = const Size(360 * 3, 640 * 3);
  await tester.pumpAndSettle();
}

void main() {
  group('FreshSeaFloor', () {
    testWidgets('is a full-width band 100 tall, excluded from semantics', (tester) async {
      await _pumpPart(tester, const Align(alignment: Alignment.bottomCenter, child: FreshSeaFloor()));

      expect(tester.takeException(), isNull);
      final size = tester.getSize(find.byType(FreshSeaFloor));
      expect(size, const Size(360, FreshSeaFloor.defaultHeight));
      expect(
        find.descendant(of: find.byType(FreshSeaFloor), matching: find.byType(ExcludeSemantics)),
        findsOneWidget,
      );
      final paint = tester.widget<CustomPaint>(
        find.descendant(of: find.byType(FreshSeaFloor), matching: find.byType(CustomPaint)),
      );
      expect(paint.painter, isA<FreshSeaFloorPainter>());
    });

    testWidgets('scales with the width and keeps its height', (tester) async {
      await _pumpPart(tester, const Align(alignment: Alignment.bottomCenter, child: FreshSeaFloor()), width: 800);

      expect(tester.takeException(), isNull);
      expect(tester.getSize(find.byType(FreshSeaFloor)), const Size(800, FreshSeaFloor.defaultHeight));
    });

    testWidgets('runs under the bottom system inset', (tester) async {
      await _pumpPart(
        tester,
        const Align(alignment: Alignment.bottomCenter, child: FreshSeaFloor(height: 90)),
        padding: const EdgeInsets.only(bottom: 24),
      );

      expect(tester.getSize(find.byType(FreshSeaFloor)).height, 114);
      expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, 640);
    });

    test('repaints only when the inset changes', () {
      const a = FreshSeaFloorPainter();
      expect(a.shouldRepaint(const FreshSeaFloorPainter()), isFalse);
      expect(a.shouldRepaint(const FreshSeaFloorPainter(inset: 20)), isTrue);
    });
  });

  group('FreshSeaFloorScroll', () {
    testWidgets('keeps the band at the bottom of a short page', (tester) async {
      await _pumpPart(tester, const FreshSeaFloorScroll(children: [SizedBox(height: 120, child: Text('Short'))]));

      expect(tester.takeException(), isNull);
      expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, 640);
    });

    testWidgets('ends a long page with the band after the content', (tester) async {
      await _pumpPart(
        tester,
        const FreshSeaFloorScroll(
          key: Key('page'),
          gap: 16,
          children: [SizedBox(height: 1200, child: Text('Long'))],
        ),
      );

      expect(find.byType(FreshSeaFloor), findsNothing);
      await tester.drag(find.byKey(const Key('page')), const Offset(0, -2000));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      final floor = find.byType(FreshSeaFloor);
      expect(floor, findsOneWidget);
      expect(tester.getBottomLeft(floor).dy, 640);
      expect(tester.getTopLeft(floor).dy, closeTo(640 - FreshSeaFloor.defaultHeight, 0.5));
      expect(tester.getBottomLeft(find.text('Long')).dy, lessThanOrEqualTo(640 - FreshSeaFloor.defaultHeight - 16));
    });
  });

  group('screens with the sea floor fit 360 x 640', () {
    testWidgets('location (pincode)', (tester) async {
      await _pumpSmall(tester, TestDeps(), const LocationScreen());

      expect(tester.takeException(), isNull);
      expect(find.byType(FreshSeaFloor), findsOneWidget);
    });

    testWidgets('not served', (tester) async {
      final deps = TestDeps();
      deps.location.onServiceability = (p) async => notServiceable(p);
      await _pumpSmall(tester, deps, const LocationScreen());

      await tester.enterText(find.byKey(const Key('pincodeField')), '600123');
      await tester.tap(find.byKey(const Key('checkPincode')));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.text('We don’t deliver to 600123 yet'), findsOneWidget);
      await tester.drag(find.byType(Scrollable).first, const Offset(0, -2000));
      await tester.pumpAndSettle();
      expect(find.byType(FreshSeaFloor), findsOneWidget);
      expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, closeTo(640, 0.5));
    });

    testWidgets('empty cart', (tester) async {
      await _pumpSmall(tester, TestDeps(pincode: '600041'), const CartScreen());

      expect(tester.takeException(), isNull);
      expect(find.text('Nothing in here yet'), findsOneWidget);
      await tester.drag(find.byType(Scrollable).first, const Offset(0, -2000));
      await tester.pumpAndSettle();
      expect(find.byType(FreshSeaFloor), findsOneWidget);
      expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, closeTo(640, 0.5));
    });

    testWidgets('help ends with the band', (tester) async {
      await _pumpSmall(tester, TestDeps(pincode: '600041'), const HelpScreen());

      expect(tester.takeException(), isNull);
      await tester.drag(find.byKey(const Key('helpPage')), const Offset(0, -4000));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.byType(FreshSeaFloor), findsOneWidget);
      expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, closeTo(640, 0.5));
    });
  });
}
