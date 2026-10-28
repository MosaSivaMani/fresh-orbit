// Order placed (D288): the teal header with the ORDER_PLACED character and the server's status, the server's
// order on the ticket card, the delivery-code note, the two buttons and the sea floor (D289). It must fit a small
// phone (360 × 640) without overflow. The owner's Padma (ORDER_PLACED, D291) stands at the right of the header,
// clear of the status and message, her waist behind the card; the drawn cast is only her fallback.

import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/features/checkout/order_success_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

Future<void> _pumpPlaced(WidgetTester tester, {Size? screen}) async {
  final deps = TestDeps(pincode: '600041', signedIn: true);
  deps.orders.onOrder = (_) async => order();
  await pumpScreen(tester, deps, const OrderSuccessScreen(orderId: 'o1'), otherPaths: ['/orders/o1']);
  if (screen != null) tester.view.physicalSize = screen * 3;
  await tester.pumpAndSettle();
}

const _placedAsset = 'assets/illustrations/order_placed.png';

/// Width and height from a PNG file's IHDR chunk.
(int, int) _pngSize(List<int> bytes) {
  int read(int at) => (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
  return (read(16), read(20));
}

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {_placedAsset}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  test('the owner\'s Padma is bundled as a PNG small enough for a phone', () {
    final file = File(_placedAsset);
    expect(file.existsSync(), isTrue);
    final bytes = file.readAsBytesSync();
    expect(bytes.sublist(1, 4), 'PNG'.codeUnits);
    expect(_pngSize(bytes), (600, 600));
    expect(bytes.length, lessThan(300 * 1024));
    expect(IllustrationSlot.orderPlaced.assetPath, _placedAsset);
  });

  testWidgets('order placed shows the server order on the ticket with the ORDER_PLACED character', (tester) async {
    await _pumpPlaced(tester);

    expect(find.byType(FreshCelebrationHeader), findsOneWidget);
    // The owner's picture, not the drawn stand-in.
    final picture = find.byKey(const ValueKey('illustration-image-ORDER_PLACED'));
    expect(picture, findsOneWidget);
    expect((tester.widget<Image>(picture).image as AssetImage).assetName, _placedAsset);
    expect(find.byKey(const ValueKey('illustration-placeholder-ORDER_PLACED')), findsNothing);
    expect(find.bySemanticsLabel('Order placed'), findsOneWidget);
    // The server's status code, shown with its label.
    expect(find.widgetWithText(FreshStatusPill, 'Confirmed'), findsOneWidget);
    expect(find.text('Thank you! Your fresh seafood is on its way.'), findsOneWidget);

    final ticket = find.byType(FreshTicketCard);
    expect(ticket, findsOneWidget);
    Finder inTicket(String text) => find.descendant(of: ticket, matching: find.text(text));
    expect(inTicket('Order FO-260001'), findsOneWidget);
    // The server's slot in the store time zone, day over time.
    expect(inTicket('Fri, 23 Oct\n2:00 pm – 4:00 pm'), findsOneWidget);
    expect(inTicket('Cash on delivery'), findsOneWidget);
    expect(inTicket('12, 3rd Cross Street, Sample Nagar, Sample City, 600041'), findsOneWidget);
    // The server's estimate (70850 paise), formatted, never recomputed from the lines.
    expect(inTicket('₹708.50'), findsOneWidget);
    expect(inTicket('The final total is set when your order is weighed and packed.'), findsOneWidget);

    final note = find.byKey(const Key('deliveryCodeNote'));
    expect(note, findsOneWidget);
    final rider = tester.widget<Illustration>(find.descendant(of: note, matching: find.byType(Illustration)));
    expect(rider.slot, IllustrationSlot.homeCardRider);
    expect(find.byType(FreshSeaFloor), findsOneWidget);
  });

  testWidgets('Padma stands at the right of the header, clear of the text, her waist behind the card', (tester) async {
    await _pumpPlaced(tester, screen: const Size(360, 640));

    final picture = tester.getRect(find.byKey(const ValueKey('illustration-image-ORDER_PLACED')));
    final size = FreshCelebrationHeader.characterSizeFor(360);
    expect(picture.size, Size.square(size));
    expect(picture.right, 360);
    // Her head is on the screen, below the top edge.
    expect(picture.top, greaterThanOrEqualTo(0));
    // The status and the message end left of her box, so the thumbs-up is never under text.
    expect(tester.getRect(find.byType(FreshStatusPill)).right, lessThanOrEqualTo(picture.left));
    expect(
      tester.getRect(find.text('Thank you! Your fresh seafood is on its way.')).right,
      lessThanOrEqualTo(picture.left),
    );
    // The card rises over her bottom edge (the cut at her waist) by 12 px only, so the fish stays in view.
    final card = tester.getRect(find.byType(FreshTicketCard));
    expect(picture.bottom - card.top, closeTo(12, 0.5));
  });

  testWidgets('without the bundled picture the drawn cast stands in', (tester) async {
    IllustrationAssets.debugSetAvailable(const {});
    await _pumpPlaced(tester, screen: const Size(360, 640));

    expect(tester.takeException(), isNull);
    expect(find.byKey(const ValueKey('illustration-placeholder-ORDER_PLACED')), findsOneWidget);
    expect(find.byKey(const ValueKey('illustration-image-ORDER_PLACED')), findsNothing);
  });

  testWidgets('Track order opens the order and Continue shopping goes home', (tester) async {
    await _pumpPlaced(tester);

    await tester.tap(find.byKey(const Key('trackOrder')));
    await tester.pumpAndSettle();
    expect(find.text('route:/orders/o1'), findsOneWidget);
  });

  testWidgets('Continue shopping goes home', (tester) async {
    await _pumpPlaced(tester);

    await tester.tap(find.byKey(const Key('continueShopping')));
    await tester.pumpAndSettle();
    expect(find.text('route:/home'), findsOneWidget);
  });

  testWidgets('fits 360 x 640 without overflow and ends with the sea floor', (tester) async {
    await _pumpPlaced(tester, screen: const Size(360, 640));

    expect(tester.takeException(), isNull);
    await tester.drag(find.byKey(const Key('orderPlacedPage')), const Offset(0, -3000));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.byKey(const Key('continueShopping')).hitTestable(), findsOneWidget);
    // The band is the last thing on the page: its bottom is the screen's bottom.
    expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, closeTo(640, 0.5));
  });

  testWidgets('fits 360 x 640 with large text', (tester) async {
    tester.platformDispatcher.textScaleFactorTestValue = 1.3;
    addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
    await _pumpPlaced(tester, screen: const Size(360, 640));

    expect(tester.takeException(), isNull);
    await tester.drag(find.byKey(const Key('orderPlacedPage')), const Offset(0, -3000));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(tester.getBottomLeft(find.byType(FreshSeaFloor)).dy, closeTo(640, 0.5));
  });
}
