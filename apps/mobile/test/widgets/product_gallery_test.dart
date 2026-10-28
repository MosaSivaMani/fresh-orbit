// Product page details from the mobile flow audit (docs/research/mobile-flow-audit-2026-10.md): every picture
// in the gallery (D262), the server's offer line (CAT-R26) and serving facts (CAT-R21), and "Kept cold" only
// when the pincode's store has cold storage (D239).

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/api/models/locations.dart';
import 'package:fresh_orbit_app/features/catalog/product_screen.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

Map<String, Object?> _image(int i) => {'url': 'https://media.test/p1/$i/1280.webp', 'altText': 'Seer fish $i'};

Map<String, Object?> _withVariant(Map<String, Object?> json, Map<String, Object?> extra) {
  final variants = [for (final v in json['variants']! as List) Map<String, Object?>.of(v as Map<String, Object?>)];
  variants.first.addAll(extra);
  return {...json, 'variants': variants};
}

void main() {
  late TestDeps deps;

  setUp(() {
    deps = TestDeps(pincode: '600041');
    deps.location.onServiceability = (_) async => serviceable();
  });

  Future<void> pump(WidgetTester tester, Map<String, Object?> json) async {
    deps.catalog.onProduct = (_, _) async => ProductDetail.fromJson(json);
    await pumpScreen(tester, deps, const ProductScreen(slug: 'seer-fish-vanjaram'));
    await tester.pumpAndSettle();
  }

  for (final count in [1, 2, 3, 4]) {
    testWidgets('$count server picture(s): every one is in the gallery, dots only for more than one', (tester) async {
      await pump(tester, {
        ...productDetailJson(),
        'images': [for (var i = 0; i < count; i++) _image(i)],
      });

      expect(find.byKey(const Key('productGallery')), findsOneWidget);
      expect(find.byKey(const ValueKey('productImage-0')), findsOneWidget);
      if (count == 1) {
        expect(find.byType(PageDots), findsNothing);
      } else {
        expect(tester.widget<PageDots>(find.byType(PageDots)).count, count);
        for (var i = 1; i < count; i++) {
          await tester.drag(find.byKey(const Key('freshGalleryPages')), const Offset(-300, 0));
          await tester.pumpAndSettle();
          expect(find.byKey(ValueKey('productImage-$i')), findsOneWidget);
          expect(tester.widget<PageDots>(find.byType(PageDots)).index, i);
        }
      }
    });
  }

  testWidgets('no picture keeps the placeholder frame and no dots', (tester) async {
    await pump(tester, productDetailJson());

    expect(find.byKey(const Key('productGallery')), findsOneWidget);
    expect(find.byType(PageDots), findsNothing);
  });

  testWidgets('shows the server offer line and serving facts as sent', (tester) async {
    await pump(tester, {
      ..._withVariant(productDetailJson(), {
        'serving': {
          'basis': {'value': 1000, 'unit': 'GRAM'},
          'netWeightGrams': 1000,
          'piecesMin': 10,
          'piecesMax': 12,
          'serves': 4,
        },
      }),
      'offer': {'couponCode': 'WELCOME100', 'text': 'Offer text from the server'},
    });

    expect(find.byKey(const Key('productOffer')), findsOneWidget);
    expect(find.text('Offer text from the server'), findsOneWidget);
    final serving = tester.widget<Text>(find.byKey(const Key('productServing'))).data!;
    expect(serving, contains('10–12'));
    expect(serving, contains('4'));
  });

  testWidgets('no offer and no serving from the server show neither', (tester) async {
    await pump(tester, productDetailJson());

    expect(find.byKey(const Key('productOffer')), findsNothing);
    expect(find.byKey(const Key('productServing')), findsNothing);
  });

  testWidgets('"Kept cold" only when the store for the pincode has cold storage', (tester) async {
    deps.location.onServiceability = (_) async {
      final json = serviceableJson();
      return Serviceability.fromJson({
        ...json,
        'fulfillmentLocation': {'id': 'l1', 'code': 'LOC1', 'name': 'Sample Store', 'hasColdStorage': false},
      });
    };
    await pump(tester, productDetailJson());

    expect(find.text('Kept cold'), findsNothing);
    expect(find.text('Net weight'), findsOneWidget);
  });

  testWidgets('cold storage at the store shows "Kept cold"', (tester) async {
    await pump(tester, productDetailJson());

    expect(find.text('Kept cold'), findsOneWidget);
  });

  test('servingText formats only what the server sends', () {
    expect(servingText(null), isNull);
    expect(servingText(Serving.fromJsonOrNull({'basis': {'value': 1, 'unit': 'PACK'}})), isNull);
    expect(
      servingText(Serving.fromJsonOrNull({'basis': {'value': 1, 'unit': 'PACK'}, 'serves': 2})),
      allOf(contains('2'), contains('1 pack')),
    );
  });
}
