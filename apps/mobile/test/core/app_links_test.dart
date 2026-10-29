// Website links opened in the app (D308, MB-4).

import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/core/app_links.dart';

void main() {
  test('website paths map to the app routes', () {
    expect(appRouteForWebsitePath(Uri.parse('https://shop.test/products/seer-fish')), '/product/seer-fish');
    expect(appRouteForWebsitePath(Uri.parse('https://shop.test/shop/prawns')), '/shop?category=prawns');
    expect(appRouteForWebsitePath(Uri.parse('https://shop.test/orders/o1/success')), '/orders/o1');
    expect(appRouteForWebsitePath(Uri.parse('https://shop.test/')), '/splash');
  });

  test('app routes are left alone', () {
    for (final path in ['/home', '/product/seer-fish', '/shop', '/orders/o1', '/cart', '/search', '/splash']) {
      expect(appRouteForWebsitePath(Uri.parse(path)), isNull, reason: path);
    }
  });
}
