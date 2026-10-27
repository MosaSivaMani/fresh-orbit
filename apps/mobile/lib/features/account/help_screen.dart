// Help and support. Answers describe how the app behaves, never a number or rule the server owns (those come
// from configuration and show on the order itself). Support contacts are business configuration: the customer
// API has no endpoint for them yet, so they come from --dart-define and the section says so when missing.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../../core/launch.dart';
import '../../widgets/bold.dart';
import '../../widgets/heading.dart';
import '../../widgets/illustrated.dart';
import '../../widgets/screen.dart';
import '../../widgets/states.dart';

const supportPhoneDefine = String.fromEnvironment('SUPPORT_PHONE');
const supportEmailDefine = String.fromEnvironment('SUPPORT_EMAIL');

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

class HelpScreen extends StatelessWidget {
  const HelpScreen({super.key, this.supportPhone = supportPhoneDefine, this.supportEmail = supportEmailDefine});

  final String supportPhone;
  final String supportEmail;

  @override
  Widget build(BuildContext context) {
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
          if (supportPhone.isEmpty && supportEmail.isEmpty)
            const Padding(
              padding: EdgeInsets.all(FreshLayout.gutter),
              child: InfoBanner(text: 'Contact details are not set up in this build of the app.'),
            ),
          if (supportPhone.isNotEmpty)
            ListTile(
              leading: const Icon(Icons.call_outlined),
              title: const Text('Call us'),
              subtitle: Text(supportPhone),
              onTap: () => openExternal(context, telUri(supportPhone)),
            ),
          if (supportEmail.isNotEmpty)
            ListTile(
              leading: const Icon(Icons.mail_outline),
              title: const Text('E-mail us'),
              subtitle: Text(supportEmail),
              onTap: () => openExternal(context, Uri(scheme: 'mailto', path: supportEmail)),
            ),
        ],
      ),
    );
  }
}
