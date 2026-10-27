import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';

void main() {
  test('the default relevance sort is left out without a search query (CAT-R15)', () {
    final query = const ProductQuery(category: 'fish').toQuery();

    expect(query.containsKey('sort'), isFalse);
    expect(query['category'], 'fish');
    expect(query['page'], 1);
  });

  test('the relevance sort is sent with a search query', () {
    expect(const ProductQuery(q: 'seer').toQuery()['sort'], 'relevance');
  });

  test('a chosen sort is always sent', () {
    expect(const ProductQuery(sort: 'price').toQuery()['sort'], 'price');
  });
}
