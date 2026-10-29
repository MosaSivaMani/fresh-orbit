// Website links opened in the app (D308, gap audit MB-4). Android App Links and iOS Universal Links hand the app the
// website address the customer tapped (an SMS, WhatsApp or the website itself). The website and the app use slightly
// different paths; this maps the website's paths to the app's routes. The domain is build configuration
// (docs/runbooks/mobile-release.md, "App links"), never a name in code (rule 7).

/// The app route for a website path, or null when the path is already an app route (go_router then matches it, or
/// shows "Page not found" with a Home button).
///
/// | Website (apps/web)        | App                       |
/// |---------------------------|---------------------------|
/// | `/`                       | `/splash` (first-run gate) |
/// | `/products/{slug}`        | `/product/{slug}`         |
/// | `/shop/{category}`        | `/shop?category={category}` |
/// | `/orders/{id}/success`    | `/orders/{id}`            |
/// | `/account/addresses` etc. | the same                  |
String? appRouteForWebsitePath(Uri uri) {
  final segments = uri.pathSegments.where((s) => s.isNotEmpty).toList();
  if (segments.isEmpty) return uri.path.isEmpty || uri.path == '/' ? '/splash' : null;
  switch (segments) {
    case ['products', final slug]:
      return Uri(path: '/product/$slug').toString();
    case ['shop', final category]:
      return Uri(path: '/shop', queryParameters: {'category': category}).toString();
    case ['orders', final id, 'success']:
      return Uri(path: '/orders/$id').toString();
  }
  return null;
}
