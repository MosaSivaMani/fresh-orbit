// Browse sizes and paging (gap audit MB-3, MB-8).

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/features/catalog/product_grid.dart';
import 'package:fresh_orbit_app/features/catalog/product_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../support/fixtures.dart';
import '../support/harness.dart';

Map<String, Object?> _variant(String id, {String status = 'IN_STOCK', int? price = 129900}) {
  final base = Map<String, Object?>.from((productDetailJson()['variants']! as List).first as Map);
  return {
    ...base,
    'id': id,
    'name': 'Size $id',
    'unitPricePaise': price,
    'availability': {'status': status, 'maxOrderableNow': null, 'message': null},
  };
}

ProductDetail _detail(List<Map<String, Object?>> variants) =>
    ProductDetail.fromJson({...productDetailJson(), 'variants': variants});

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  group('the size the product page opens on (MB-3)', () {
    test('a sold-out first size is skipped for one in stock', () {
      final d = _detail([_variant('small', status: 'OUT_OF_STOCK'), _variant('medium')]);
      expect(initialVariant(d, null).id, 'medium');
    });

    test('the card\'s default size wins when it can be ordered', () {
      final d = _detail([_variant('small'), _variant('medium')]);
      expect(initialVariant(d, 'medium').id, 'medium');
    });

    test('a sold-out default size falls back to one in stock', () {
      final d = _detail([_variant('small', status: 'OUT_OF_STOCK'), _variant('medium', status: 'OUT_OF_STOCK'), _variant('large')]);
      expect(initialVariant(d, 'medium').id, 'large');
    });

    test('a size not sold here is skipped; with nothing orderable the first size shows', () {
      expect(initialVariant(_detail([_variant('a', price: null), _variant('b')]), null).id, 'b');
      expect(
        initialVariant(_detail([_variant('a', status: 'OUT_OF_STOCK'), _variant('b', status: 'OUT_OF_STOCK')]), null).id,
        'a',
      );
    });
  });

  group('product cards grow with the phone\'s text size (MB-6)', () {
    final product = ProductSummary.fromJson({
      'id': 'p1',
      'slug': 'seer',
      'name': 'Seer Fish (Vanjaram) Premium Steaks Pack',
      'localNames': ['Neymeen', 'Surmai', 'Vanjaram'],
      'fromPrice': {'unitPricePaise': 129900, 'compareAtPaise': 149900, 'perUnit': 'KG'},
      'availability': {'status': 'IN_STOCK', 'message': null},
      'locationRequired': false,
    });

    for (final scale in [1.0, 1.3, 1.5, 2.0]) {
      testWidgets('no overflow at 360 x 640 with text scale $scale', (tester) async {
        final deps = TestDeps(pincode: '600041');
        await pumpScreen(
          tester,
          deps,
          Builder(
            builder: (context) => MediaQuery(
              data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(scale)),
              child: Scaffold(
                body: Builder(
                  builder: (context) => GridView.builder(
                    padding: const EdgeInsets.all(16),
                    gridDelegate: productGridDelegateOf(context),
                    itemCount: 4,
                    itemBuilder: (_, i) => ShopProductCard(product: product, index: i),
                  ),
                ),
              ),
            ),
          ),
        );
        tester.view.physicalSize = const Size(360 * 3, 640 * 3);
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
        expect(find.byType(ShopProductCard), findsWidgets);
      });
    }
  });

  group('load more listens to the grid only (MB-8)', () {
    ScrollMetrics metrics(Axis axis, double after) => FixedScrollMetrics(
      minScrollExtent: 0,
      maxScrollExtent: 1000,
      pixels: 1000 - after,
      viewportDimension: 600,
      axisDirection: axis == Axis.vertical ? AxisDirection.down : AxisDirection.right,
      devicePixelRatio: 3,
    );

    testWidgets('a sideways swipe of the category row never loads the next page', (tester) async {
      await tester.pumpWidget(const SizedBox());
      final context = tester.element(find.byType(SizedBox));
      // A new notification has depth 0 (the scrollable right under the listener).
      ScrollUpdateNotification note(Axis axis, double after) =>
          ScrollUpdateNotification(metrics: metrics(axis, after), context: context);

      expect(isGridScrollNearEnd(note(Axis.vertical, 100)), isTrue);
      expect(isGridScrollNearEnd(note(Axis.vertical, 900)), isFalse);
      expect(isGridScrollNearEnd(note(Axis.horizontal, 100)), isFalse);
    });
  });
}
