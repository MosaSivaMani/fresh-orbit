// Help and support. Answers describe how the app behaves, never a number or rule the server owns (those come
// from configuration and show on the order itself). Support contacts are business configuration read from the
// public business profile (GET /business-profile); the section says so when the owner has not set them up.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../../api/models/catalog.dart';
import '../../core/launch.dart';
import '../../state/catalog_state.dart';
import '../../state/core_providers.dart';
import '../../widgets/illustrated.dart';
import '../../widgets/policy_links.dart';

const _faq = <(String, String)>[
  (
    'Can my total change after weighing?',
    'Only down, never up. Fish and meat are weighed after cleaning when your order is packed. If your pack weighs '
        'less than you ordered, you pay less (or, if you paid online, we refund the difference). If it weighs more, '
        'the extra is free.',
  ),
  (
    'What does the cleaning yield mean?',
    'Cleaning removes parts such as the head, skin or bones. The product page says how much of the whole weight '
        'usually remains. You pay only for the net weight.',
  ),
  (
    'How do I pay?',
    'Choose a payment method at checkout. Cash or UPI on delivery is paid to the delivery partner when your order '
        'arrives.',
  ),
  (
    'What is the delivery code?',
    'When your order is out for delivery, the order page shows a code. Share it with the delivery partner only '
        'when you have your order.',
  ),
  ('Can I cancel my order?', 'Open the order. While it can still be cancelled, the page shows a Cancel order button.'),
  (
    'Something was wrong with my order',
    'Open the delivered order and choose Report a problem. Our team will get back to you.',
  ),
];

class HelpScreen extends ConsumerWidget {
  const HelpScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(businessProfileProvider);
    return Scaffold(
      appBar: const PageHeader(text: 'Help and', brushed: 'support'),
      // The sea floor ends the page (D289).
      body: FreshSeaFloorScroll(
        key: const Key('helpPage'),
        gap: FreshLayout.sectionGap,
        children: [
          const Padding(
            padding: EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, FreshLayout.sectionGap),
            child: HelpHero(message: 'Find a quick answer below, or contact our team.'),
          ),
          const Padding(
            padding: FreshLayout.page,
            child: PageTitle(text: 'Quick', brushed: 'answers', size: 20),
          ),
          const SizedBox(height: 8),
          Padding(
            padding: FreshLayout.page,
            child: SectionBox(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final (q, a) in _faq)
                    ExpansionTile(
                      title: Text(q),
                      childrenPadding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, 12),
                      expandedAlignment: Alignment.centerLeft,
                      children: [Text(a)],
                    ),
                ],
              ),
            ),
          ),
          const SizedBox(height: FreshLayout.sectionGap),
          const Padding(
            padding: FreshLayout.page,
            child: PageTitle(text: 'Contact', brushed: 'us', size: 20),
          ),
          const SizedBox(height: 8),
          Padding(
            key: const Key('helpContacts'),
            padding: FreshLayout.page,
            child: switch (profile) {
              AsyncData(:final value) => _Contacts(profile: value),
              AsyncError() => Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const InfoBanner(text: 'We could not load our contact details.'),
                  TextButton(
                    key: const Key('retryContacts'),
                    onPressed: () => ref.invalidate(businessProfileProvider),
                    child: const Text('Try again'),
                  ),
                ],
              ),
              _ => const SkeletonBox(height: 56),
            },
          ),
          // The policy pages on the website (D299); left out when the website address is not configured.
          if (ref.watch(policyLinksProvider).isNotEmpty) ...[
            const SizedBox(height: FreshLayout.sectionGap),
            const Padding(
              padding: FreshLayout.page,
              child: PageTitle(text: 'Our', brushed: 'policies', size: 20),
            ),
            const SizedBox(height: 8),
            const PolicyMenuCard(key: Key('helpPolicies')),
          ],
          // The grievance officer from the business profile (D374); left out until the owner names one.
          if (profile.value?.grievanceOfficer case final officer?) ...[
            const SizedBox(height: FreshLayout.sectionGap),
            const Padding(
              padding: FreshLayout.page,
              child: PageTitle(text: 'Grievance', brushed: 'officer', size: 20),
            ),
            const SizedBox(height: 8),
            Padding(padding: FreshLayout.page, child: _GrievanceOfficer(officer: officer)),
          ],
          if (profile.value case final p? when _StoreDetails.hasAny(p)) ...[
            const SizedBox(height: FreshLayout.sectionGap),
            const Padding(
              padding: FreshLayout.page,
              child: PageTitle(text: 'About the', brushed: 'store', size: 20),
            ),
            const SizedBox(height: 8),
            Padding(padding: FreshLayout.page, child: _StoreDetails(profile: p)),
          ],
        ],
      ),
    );
  }
}

/// Call and e-mail rows from the business profile; a note when the owner has not set them up yet.
class _Contacts extends StatelessWidget {
  const _Contacts({required this.profile});

  final BusinessProfile profile;

  @override
  Widget build(BuildContext context) {
    final phone = profile.supportPhone ?? '';
    final email = profile.supportEmail ?? '';
    final hours = profile.supportHours;
    if (phone.isEmpty && email.isEmpty) {
      return const InfoBanner(text: 'Our contact details are not set up yet. Please try again later.');
    }
    return SectionBox(
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          if (phone.isNotEmpty)
            ListTile(
              key: const Key('callSupport'),
              leading: const Icon(Icons.call_outlined),
              title: const Text('Call us'),
              subtitle: Text(hours == null ? phone : '$phone · $hours'),
              onTap: () => openExternal(context, telUri(phone)),
            ),
          if (email.isNotEmpty)
            ListTile(
              key: const Key('emailSupport'),
              leading: const Icon(Icons.mail_outline),
              title: const Text('E-mail us'),
              subtitle: Text(email),
              onTap: () => openExternal(context, Uri(scheme: 'mailto', path: email)),
            ),
        ],
      ),
    );
  }
}

/// The grievance officer's name, e-mail, phone and response time, exactly as the business profile holds them (D374).
class _GrievanceOfficer extends StatelessWidget {
  const _GrievanceOfficer({required this.officer});

  final GrievanceOfficer officer;

  @override
  Widget build(BuildContext context) {
    final designation = officer.designation;
    final phone = officer.phone;
    final responseTime = officer.responseTime;
    return SectionBox(
      key: const Key('helpGrievanceOfficer'),
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          ListTile(
            leading: const Icon(Icons.person_outline),
            title: Text(officer.name),
            subtitle: designation == null ? null : Text(designation),
          ),
          ListTile(
            key: const Key('emailGrievanceOfficer'),
            leading: const Icon(Icons.mail_outline),
            title: Text(officer.email),
            onTap: () => openExternal(context, Uri(scheme: 'mailto', path: officer.email)),
          ),
          if (phone != null)
            ListTile(
              key: const Key('callGrievanceOfficer'),
              leading: const Icon(Icons.call_outlined),
              title: Text(phone),
              onTap: () => openExternal(context, telUri(phone)),
            ),
          if (responseTime != null)
            ListTile(leading: const Icon(Icons.schedule_outlined), title: Text(responseTime)),
        ],
      ),
    );
  }
}

/// The legal details from the business profile (legal name, registered address, GSTIN, FSSAI licence), as
/// configured by the owner (D273). Rows the owner has not filled are left out.
class _StoreDetails extends StatelessWidget {
  const _StoreDetails({required this.profile});

  final BusinessProfile profile;

  static bool hasAny(BusinessProfile p) =>
      [p.legalName, p.address, p.gstin, p.fssaiLicenseNo].any((v) => v != null && v.trim().isNotEmpty);

  @override
  Widget build(BuildContext context) {
    final rows = <(String, String?)>[
      ('Legal name', profile.legalName),
      ('Registered address', profile.address),
      ('GSTIN', profile.gstin),
      ('FSSAI licence', profile.fssaiLicenseNo),
    ];
    return SectionBox(
      key: const Key('helpStoreDetails'),
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          for (final (label, value) in rows)
            if (value != null && value.trim().isNotEmpty)
              ListTile(title: Text(label), subtitle: SelectableText(value)),
        ],
      ),
    );
  }
}
