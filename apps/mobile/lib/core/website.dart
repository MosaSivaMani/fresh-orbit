// The store's website (D299): the app opens the privacy policy, terms and the other policies there, in the browser.
// The address is build configuration, like the API address: `--dart-define=WEBSITE_BASE_URL=https://shop.example.in`.
// Empty or not a web address means not configured: every policy link is hidden, never guessed (rule 7).

import 'package:flutter/material.dart';

/// `--dart-define=WEBSITE_BASE_URL=https://shop.example.in` (origin, optionally with a path prefix).
const String websiteBaseUrlDefine = String.fromEnvironment('WEBSITE_BASE_URL');

/// The website's policy pages (`apps/web/src/app/support/<path>`), in the order the app lists them.
enum PolicyPage {
  privacy('PRIVACY', 'Privacy policy', 'support/privacy-policy', Icons.privacy_tip_outlined),
  terms('TERMS', 'Terms of use', 'support/terms', Icons.gavel_rounded),
  refund('REFUND', 'Refund policy', 'support/refund-policy', Icons.currency_rupee_rounded),
  cancellation('CANCELLATION', 'Cancellation policy', 'support/cancellation-policy', Icons.event_busy_outlined),
  delivery('DELIVERY', 'Delivery policy', 'support/delivery-policy', Icons.local_shipping_outlined);

  const PolicyPage(this.code, this.title, this.path, this.icon);

  /// Stable code used in widget keys (`policy-PRIVACY`).
  final String code;
  final String title;
  final String path;
  final IconData icon;
}

/// The address of [page] on the website at [base], or null when [base] is not an http(s) address.
Uri? policyUri(String base, PolicyPage page) {
  final origin = Uri.tryParse(base.trim());
  if (origin == null || !(origin.scheme == 'https' || origin.scheme == 'http') || origin.host.isEmpty) return null;
  final prefix = origin.path.endsWith('/') ? origin.path : '${origin.path}/';
  return Uri(
    scheme: origin.scheme,
    host: origin.host,
    port: origin.hasPort ? origin.port : null,
    path: '$prefix${page.path}',
  );
}

/// Every policy page's address, or an empty map when the website is not configured.
Map<PolicyPage, Uri> policyLinks(String base) {
  final links = <PolicyPage, Uri>{};
  for (final page in PolicyPage.values) {
    final uri = policyUri(base, page);
    if (uri == null) return const {};
    links[page] = uri;
  }
  return links;
}
