import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_orbit_app/widgets/illustrated.dart';
import 'package:fresh_orbit_app/widgets/motion.dart';
import 'package:fresh_orbit_app/widgets/states.dart';

Future<void> _pump(WidgetTester tester, Widget child, {bool reduceMotion = false, ThemeData? theme}) async {
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = const Size(400 * 3, 1600 * 3);
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: theme ?? AppTheme.light(),
      builder: (context, app) => MediaQuery(
        data: MediaQuery.of(context).copyWith(disableAnimations: reduceMotion),
        child: app!,
      ),
      home: Scaffold(body: SingleChildScrollView(child: child)),
    ),
  );
}

void main() {
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  group('IllustrationSlot', () {
    test('asset path is the lower-snake slot code under assets/illustrations', () {
      expect(IllustrationSlot.onboardingFresh.code, 'ONBOARDING_FRESH');
      expect(IllustrationSlot.onboardingFresh.assetPath, 'assets/illustrations/onboarding_fresh.png');
      expect(IllustrationSlot.featureCleanedYourWay.assetPath, 'assets/illustrations/feature_cleaned_your_way.png');
      for (final slot in IllustrationSlot.values) {
        expect(slot.code, matches(RegExp(r'^[A-Z]+(_[A-Z]+)*$')), reason: slot.name);
        expect(slot.code.startsWith('ADMIN_'), isFalse, reason: 'admin slots are not used in mobile');
        expect(slot.semanticLabel, isNotEmpty, reason: slot.name);
      }
      expect(IllustrationSlot.values.map((s) => s.code).toSet(), hasLength(IllustrationSlot.values.length));
    });

    test('tracking illustration follows the order status', () {
      expect(slotForOrderStatus('CONFIRMED'), IllustrationSlot.orderPlaced);
      expect(slotForOrderStatus('PENDING_PAYMENT'), IllustrationSlot.orderPlaced);
      expect(slotForOrderStatus('PREPARING'), IllustrationSlot.orderPacking);
      expect(slotForOrderStatus('PACKED'), IllustrationSlot.orderPacking);
      expect(slotForOrderStatus('OUT_FOR_DELIVERY'), IllustrationSlot.orderOnTheWay);
      expect(slotForOrderStatus('DELIVERED'), IllustrationSlot.orderDelivered);
      expect(slotForOrderStatus('CANCELLED'), IllustrationSlot.orderCancelled);
      expect(slotForOrderStatus('SOMETHING_NEW'), IllustrationSlot.orderPlaced);
    });
  });

  group('Illustration', () {
    testWidgets('falls back to the drawn placeholder when the image is not bundled', (tester) async {
      IllustrationAssets.debugSetAvailable(const {});
      await _pump(tester, const Illustration(slot: IllustrationSlot.emptyCart, size: 120));

      expect(find.byKey(const ValueKey('illustration-placeholder-EMPTY_CART')), findsOneWidget);
      expect(find.byType(Image), findsNothing);
      expect(tester.getSize(find.byType(Illustration)), const Size(120, 120));
    });

    testWidgets('loads the slot PNG when the bundle has it', (tester) async {
      IllustrationAssets.debugSetAvailable(const {
        'assets/illustrations/empty_cart.png',
        'assets/illustrations/empty_cart.webp',
      });
      await _pump(tester, const Illustration(slot: IllustrationSlot.emptyCart));

      final image = tester.widget<Image>(find.byKey(const ValueKey('illustration-image-EMPTY_CART')));
      expect((image.image as AssetImage).assetName, 'assets/illustrations/empty_cart.png');
    });

    testWidgets('reads a WebP when no PNG is bundled', (tester) async {
      IllustrationAssets.debugSetAvailable(const {'assets/illustrations/empty_cart.webp'});
      await _pump(tester, const Illustration(slot: IllustrationSlot.emptyCart));

      final image = tester.widget<Image>(find.byKey(const ValueKey('illustration-image-EMPTY_CART')));
      expect((image.image as AssetImage).assetName, 'assets/illustrations/empty_cart.webp');
    });

    testWidgets('every slot draws a cast placeholder in light and dark', (tester) async {
      IllustrationAssets.debugSetAvailable(const {});
      for (final theme in [AppTheme.light(), AppTheme.dark()]) {
        await _pump(
          tester,
          Wrap(children: [for (final slot in IllustrationSlot.values) Illustration(slot: slot, size: 96)]),
          theme: theme,
        );
        for (final slot in IllustrationSlot.values) {
          expect(find.byKey(ValueKey('illustration-placeholder-${slot.code}')), findsOneWidget);
        }
        expect(tester.takeException(), isNull);
      }
    });

    testWidgets('a missing file listed in the bundle still ends on the placeholder', (tester) async {
      IllustrationAssets.debugSetAvailable(const {'assets/illustrations/offline.webp'});
      await _pump(tester, const Illustration(slot: IllustrationSlot.offline));
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 50)));
      await tester.pump();

      expect(find.byKey(const ValueKey('illustration-placeholder-OFFLINE')), findsOneWidget);
    });

    testWidgets('has the slot alt text, or no semantics when excluded', (tester) async {
      IllustrationAssets.debugSetAvailable(const {});
      final handle = tester.ensureSemantics();
      await _pump(
        tester,
        const Column(
          children: [
            Illustration(slot: IllustrationSlot.helpSupport),
            Illustration(slot: IllustrationSlot.offline, excludeFromSemantics: true),
            Illustration(slot: IllustrationSlot.notFound, semanticLabel: 'Lost fish'),
          ],
        ),
      );

      expect(find.bySemanticsLabel(IllustrationSlot.helpSupport.semanticLabel), findsOneWidget);
      expect(find.bySemanticsLabel(IllustrationSlot.offline.semanticLabel), findsNothing);
      expect(find.bySemanticsLabel('Lost fish'), findsOneWidget);
      handle.dispose();
    });
  });

  group('EmptyState', () {
    testWidgets('shows the slot illustration, title, message and action', (tester) async {
      IllustrationAssets.debugSetAvailable(const {});
      var tapped = 0;
      await _pump(
        tester,
        EmptyState(
          slot: IllustrationSlot.emptyWishlist,
          title: 'Your wishlist is empty',
          message: 'Tap the heart on a product to save it for later.',
          action: FilledButton(onPressed: () => tapped++, child: const Text('Browse fish')),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('illustration-placeholder-EMPTY_WISHLIST')), findsOneWidget);
      expect(find.text('Your wishlist is empty'), findsOneWidget);
      expect(find.text('Tap the heart on a product to save it for later.'), findsOneWidget);
      await tester.tap(find.text('Browse fish'));
      expect(tapped, 1);
    });

    testWidgets('the illustration is decorative because the title says the same', (tester) async {
      IllustrationAssets.debugSetAvailable(const {});
      final handle = tester.ensureSemantics();
      await _pump(tester, const EmptyState(slot: IllustrationSlot.emptyCart, title: 'Your cart is empty'));
      await tester.pumpAndSettle();

      expect(find.bySemanticsLabel(IllustrationSlot.emptyCart.semanticLabel), findsNothing);
      expect(find.bySemanticsLabel('Your cart is empty'), findsOneWidget);
      handle.dispose();
    });
  });

  group('ReceiptCard', () {
    testWidgets('shows exactly the values it is given', (tester) async {
      await _pump(
        tester,
        const ReceiptCard(
          title: 'Order FO-260001',
          rows: [ReceiptRow('Delivery', '23 Oct, 2:00 PM – 4:00 PM'), ReceiptRow('Payment', 'Cash on delivery')],
          total: ReceiptRow('Estimated total', '₹708.50'),
          note: 'The final total is set when your order is weighed and packed.',
        ),
      );

      final texts = tester
          .widgetList<Text>(find.descendant(of: find.byType(ReceiptCard), matching: find.byType(Text)))
          .map((t) => t.data)
          .toList();
      expect(texts, [
        'Order FO-260001',
        'Delivery',
        '23 Oct, 2:00 PM – 4:00 PM',
        'Payment',
        'Cash on delivery',
        'Estimated total',
        '₹708.50',
        'The final total is set when your order is weighed and packed.',
      ]);
    });

    testWidgets('reads each row as one phrase, with the given total label', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(
        tester,
        const ReceiptCard(
          title: 'Order FO-1',
          rows: [ReceiptRow('Payment', 'UPI on delivery')],
          total: ReceiptRow('Estimated total', '₹99.00', semanticsLabel: 'Estimated total ₹99.00'),
        ),
      );

      expect(find.bySemanticsLabel('Payment: UPI on delivery'), findsOneWidget);
      expect(find.bySemanticsLabel('Estimated total ₹99.00'), findsOneWidget);
      handle.dispose();
    });
  });

  group('reduced motion', () {
    testWidgets('entrance motion runs and settles when motion is allowed', (tester) async {
      await _pump(tester, const EntranceMotion(child: Text('Hello')));

      expect(tester.hasRunningAnimations, isTrue);
      expect(find.ancestor(of: find.text('Hello'), matching: find.byType(Opacity)), findsOneWidget);
      await tester.pumpAndSettle();
      expect(tester.hasRunningAnimations, isFalse);
    });

    testWidgets('no animation when the platform asks for reduced motion', (tester) async {
      IllustrationAssets.debugSetAvailable(const {});
      await _pump(
        tester,
        const Column(
          children: [
            EntranceMotion(child: Text('Hello')),
            PolaroidCard(
              picture: SizedBox(width: 80, height: 80),
              stickers: [Sticker(label: 'Fresh')],
            ),
            EmptyState(slot: IllustrationSlot.emptyOrders, title: 'No orders yet'),
          ],
        ),
        reduceMotion: true,
      );

      expect(tester.hasRunningAnimations, isFalse);
      expect(find.ancestor(of: find.text('Hello'), matching: find.byType(Opacity)), findsNothing);
      expect(find.text('No orders yet'), findsOneWidget);
    });
  });

  group('illustrated widgets', () {
    testWidgets('feature card, sticker, help hero and polaroid render in light and dark', (tester) async {
      IllustrationAssets.debugSetAvailable(const {});
      for (final theme in [AppTheme.light(), AppTheme.dark()]) {
        await _pump(
          tester,
          const Column(
            children: [
              FeatureCard(
                slot: IllustrationSlot.featureColdChain,
                number: 3,
                title: 'Cold all the way',
                message: 'Kept chilled from our store to your door.',
              ),
              HelpHero(),
              PolaroidCard(picture: SizedBox(width: 80, height: 80), caption: 'Fresh catch'),
              Sticker(label: 'Kept cold', tone: StickerTone.teal),
            ],
          ),
          theme: theme,
        );
        await tester.pumpAndSettle();

        expect(find.text('03'), findsOneWidget);
        expect(find.text('Cold all the way'), findsOneWidget);
        expect(find.text('How can we help you today?'), findsOneWidget);
        expect(find.byKey(const ValueKey('illustration-placeholder-HELP_SUPPORT')), findsOneWidget);
        expect(find.text('Kept cold'), findsOneWidget);
        expect(tester.takeException(), isNull);
      }
    });
  });
}
