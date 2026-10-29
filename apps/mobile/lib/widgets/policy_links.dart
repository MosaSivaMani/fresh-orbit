// Policy links (D299, MA-3, ME-1): the sign-in consent line and the policy rows in Settings and Help open the
// store website's pages in the browser. They read the configured website address; when it is not configured the
// rows are left out and the consent line has no links (rule 7, never a guessed address).

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../core/website.dart';
import '../state/core_providers.dart';

/// "By continuing you agree to our terms and privacy policy." with "terms" and "privacy policy" as links.
class PolicyConsentLine extends ConsumerWidget {
  const PolicyConsentLine({super.key, this.style});

  final TextStyle? style;

  static const _sentence = 'By continuing you agree to our terms and privacy policy.';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final links = ref.watch(policyLinksProvider);
    final terms = links[PolicyPage.terms];
    final privacy = links[PolicyPage.privacy];
    if (terms == null || privacy == null) {
      return Text(_sentence, key: const Key('policyConsent'), textAlign: TextAlign.center, style: style);
    }
    final open = ref.read(externalLauncherProvider);
    return FreshLinkText(
      key: const Key('policyConsent'),
      textAlign: TextAlign.center,
      style: style,
      parts: [
        const FreshTextPart('By continuing you agree to our '),
        FreshTextPart.link('terms', onTap: () => open(context, terms)),
        const FreshTextPart(' and '),
        FreshTextPart.link('privacy policy', onTap: () => open(context, privacy)),
        const FreshTextPart('.'),
      ],
    );
  }
}

/// One menu row per policy page (privacy, terms, refund, cancellation, delivery); nothing when the website is not
/// configured. Each row opens its page in the browser.
class PolicyMenuCard extends ConsumerWidget {
  const PolicyMenuCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final links = ref.watch(policyLinksProvider);
    if (links.isEmpty) return const SizedBox.shrink();
    final open = ref.read(externalLauncherProvider);
    return MenuCard(
      children: [
        for (final MapEntry(key: page, value: uri) in links.entries)
          MenuRow(
            key: Key('policy-${page.code}'),
            icon: page.icon,
            title: page.title,
            trailing: Icon(Icons.open_in_new_rounded, size: 16, color: illustratedColors(context).muted),
            onTap: () => open(context, uri),
          ),
      ],
    );
  }
}
