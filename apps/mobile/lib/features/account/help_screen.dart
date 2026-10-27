// Help and support. Answers describe how the app behaves, never a number or rule the server owns (those come
// from configuration and show on the order itself). Support contacts are business configuration read from the
// public business profile (GET /business-profile); the section says so when the owner has not set them up.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../../api/models/catalog.dart';
import '../../core/launch.dart';
import '../../state/catalog_state.dart';
import '../../widgets/illustrated.dart';

const _faq = <(String, String)>[
  (
    'Why is my total an estimate?',
    'Fish and meat are weighed when your order is packed. You pay for the net weight we pack, and never more than '
        'the maximum shown at checkout.',
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
      body: ListView(
        padding: const EdgeInsets.only(bottom: FreshLayout.sectionGap),
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
