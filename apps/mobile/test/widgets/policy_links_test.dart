// Policy links (D299, MA-3, ME-1): the sign-in consent line, Settings and Help open the website's policy pages
// with the configured address, and every link is hidden when the address is not configured (rule 7).

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/core/website.dart';
import 'package:fresh_orbit_app/features/account/help_screen.dart';
import 'package:fresh_orbit_app/features/account/settings_screen.dart';
import 'package:fresh_orbit_app/features/auth/login_screen.dart';
import 'package:fresh_orbit_app/state/core_providers.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../support/harness.dart';

const _site = 'https://shop.example.test';
const _sentence = 'By continuing you agree to our terms and privacy policy.';

/// Overrides that configure the website and record every address the app asks to open.
List<Override> _configured(List<Uri> opened, {String site = _site}) => [
  websiteBaseUrlProvider.overrideWithValue(site),
  externalLauncherProvider.overrideWithValue((context, uri) async => opened.add(uri)),
];

/// Records opened addresses without configuring the website (the define is empty in tests).
List<Override> _notConfigured(List<Uri> opened) => [
  externalLauncherProvider.overrideWithValue((context, uri) async => opened.add(uri)),
];

void main() {
  group('website address', () {
    test('builds each policy page under the configured origin', () {
      expect(policyUri(_site, PolicyPage.privacy), Uri.parse('$_site/support/privacy-policy'));
      expect(policyUri('$_site/', PolicyPage.terms), Uri.parse('$_site/support/terms'));
      expect(policyUri('$_site/shop?x=1', PolicyPage.refund), Uri.parse('$_site/shop/support/refund-policy'));
      expect(policyLinks(_site).keys, PolicyPage.values);
    });

    test('an empty or non-web address gives no links', () {
      expect(policyLinks(''), isEmpty);
      expect(policyLinks('   '), isEmpty);
      expect(policyLinks('shop.example.test'), isEmpty);
      expect(policyLinks('ftp://shop.example.test'), isEmpty);
    });
  });

  group('FreshLinkText', () {
    testWidgets('only the link words run their tap', (tester) async {
      var taps = 0;
      await pumpScreen(
        tester,
        TestDeps(),
        Scaffold(
          body: Center(
            child: FreshLinkText(
              parts: [
                const FreshTextPart('Read the '),
                FreshTextPart.link('rules', onTap: () => taps++),
                const FreshTextPart(' first.'),
              ],
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Read the rules first.', findRichText: true), findsOneWidget);
      await tester.tapOnText(find.textRange.ofSubstring('first'));
      expect(taps, 0);
      await tester.tapOnText(find.textRange.ofSubstring('rules'));
      expect(taps, 1);
    });
  });

  group('sign-in consent line', () {
    testWidgets('"terms" and "privacy policy" open the configured website pages', (tester) async {
      final opened = <Uri>[];
      await pumpScreen(tester, TestDeps(), const LoginScreen(), overrides: _configured(opened));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);

      await tester.ensureVisible(find.byKey(const Key('policyConsent')));
      await tester.pumpAndSettle();
      expect(find.byType(FreshLinkText), findsOneWidget);
      expect(find.text(_sentence, findRichText: true), findsOneWidget);

      await tester.tapOnText(find.textRange.ofSubstring('terms'));
      await tester.tapOnText(find.textRange.ofSubstring('privacy policy'));
      expect(opened, [Uri.parse('$_site/support/terms'), Uri.parse('$_site/support/privacy-policy')]);

      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('without a website address the line is plain text with no links', (tester) async {
      final opened = <Uri>[];
      await pumpScreen(tester, TestDeps(), const LoginScreen(), overrides: _notConfigured(opened));
      await tester.pumpAndSettle();

      expect(find.byType(FreshLinkText), findsNothing);
      expect(find.text(_sentence), findsOneWidget);
      await tester.ensureVisible(find.text(_sentence));
      await tester.tap(find.text(_sentence));
      expect(opened, isEmpty);

      await tester.pumpWidget(const SizedBox());
    });
  });

  group('settings', () {
    testWidgets('lists every policy and opens the one tapped', (tester) async {
      final opened = <Uri>[];
      await pumpScreen(tester, TestDeps(), const SettingsScreen(), overrides: _configured(opened));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('settingsPolicies')), findsOneWidget);
      for (final page in PolicyPage.values) {
        expect(find.byKey(Key('policy-${page.code}')), findsOneWidget, reason: page.code);
      }
      // "Privacy and terms" no longer sends the customer to Help.
      expect(find.text('Privacy and terms'), findsNothing);

      await tester.ensureVisible(find.byKey(const Key('policy-PRIVACY')));
      await tester.tap(find.byKey(const Key('policy-PRIVACY')));
      await tester.ensureVisible(find.byKey(const Key('policy-REFUND')));
      await tester.tap(find.byKey(const Key('policy-REFUND')));
      expect(opened, [Uri.parse('$_site/support/privacy-policy'), Uri.parse('$_site/support/refund-policy')]);
    });

    testWidgets('leaves the policies out when the website is not configured', (tester) async {
      final opened = <Uri>[];
      await pumpScreen(tester, TestDeps(), const SettingsScreen(), overrides: _notConfigured(opened));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('settingsPolicies')), findsNothing);
      expect(find.byKey(const Key('policy-PRIVACY')), findsNothing);
      expect(find.text('Privacy policy'), findsNothing);
    });
  });

  group('help', () {
    testWidgets('lists every policy and opens the one tapped', (tester) async {
      final opened = <Uri>[];
      await pumpScreen(tester, TestDeps(pincode: '600041'), const HelpScreen(), overrides: _configured(opened));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('helpPolicies')), findsOneWidget);
      for (final page in PolicyPage.values) {
        expect(find.byKey(Key('policy-${page.code}')), findsOneWidget, reason: page.code);
      }
      await tester.ensureVisible(find.byKey(const Key('policy-DELIVERY')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('policy-DELIVERY')));
      expect(opened, [Uri.parse('$_site/support/delivery-policy')]);
    });

    testWidgets('leaves the policies out when the website is not configured', (tester) async {
      await pumpScreen(tester, TestDeps(pincode: '600041'), const HelpScreen(), overrides: _notConfigured([]));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('helpPolicies')), findsNothing);
      expect(find.byKey(const Key('policy-TERMS')), findsNothing);
    });
  });
}
