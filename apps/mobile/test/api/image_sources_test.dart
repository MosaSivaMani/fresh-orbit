// D375 (audit W-9): product list items and cart lines carry the 320 / 640 / 1280 px renditions, and a card picks the
// smallest that fills its drawn width at the screen's pixel density.

import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/cart.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_theme/fresh_theme.dart';

const _sources = [
  {'width': 320, 'url': 'https://api.test/media/p1/320.webp'},
  {'width': 640, 'url': 'https://api.test/media/p1/640.webp'},
  {'width': 1280, 'url': 'https://api.test/media/p1/1280.webp'},
];

void main() {
  test('a product list item reads its image renditions', () {
    final item = ProductSummary.fromJson({
      'id': 'p1',
      'slug': 'seer-fish',
      'name': 'Seer Fish',
      'localNames': <String>[],
      'imageUrl': 'https://api.test/media/p1/640.webp',
      'imageSources': _sources,
      'locationRequired': true,
    });

    expect(item.imageSources.map((s) => s.width), [320, 640, 1280]);
  });

  test('an item without renditions has none', () {
    final item = ProductSummary.fromJson({
      'id': 'p1',
      'slug': 'seer-fish',
      'name': 'Seer Fish',
      'localNames': <String>[],
      'locationRequired': true,
    });

    expect(item.imageSources, isEmpty);
  });

  test('a cart line reads its image renditions', () {
    final line = CartLine.fromJson({
      'id': 'l1',
      'productId': 'p1',
      'productSlug': 'seer-fish',
      'productName': 'Seer Fish',
      'variantId': 'v1',
      'variantName': 'Steaks',
      'sellUnit': 'KG',
      'imageUrl': 'https://api.test/media/p1/320.webp',
      'imageSources': _sources,
      'quantity': {'value': 500, 'unit': 'GRAM'},
      'estimatedNetQuantity': {'value': 500, 'unit': 'GRAM'},
      'maxChargeableQuantity': {'value': 500, 'unit': 'GRAM'},
      'unitPricePaise': 129900,
      'priceUnit': 'KG',
      'itemPaise': 64950,
      'estimatedLineTotalPaise': 64950,
      'maxLineTotalPaise': 64950,
      'issues': <Object>[],
    });

    expect(line.imageSources.map((s) => s.url), _sources.map((s) => s['url']));
  });

  test('the smallest rendition that fills the drawn pixels is chosen, else the largest', () {
    const sources = [
      PhotoSource(width: 1280, url: 'l'),
      PhotoSource(width: 320, url: 's'),
      PhotoSource(width: 640, url: 'm'),
    ];

    expect(pickPhotoSource(sources, 96 * 3), 's');
    expect(pickPhotoSource(sources, 180 * 3), 'm');
    expect(pickPhotoSource(sources, 400 * 3), 'l');
    expect(pickPhotoSource(sources, double.infinity), 'l');
    expect(pickPhotoSource(const [], 100), isNull);
  });
}
