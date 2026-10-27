// Illustrated building blocks (D53): Sticker, PolaroidCard, FeatureCard, HelpHero and ReceiptCard. They lay out
// what they are given; none of them computes a business value (CLAUDE.md rule 4).

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../illustrations/illustration.dart';
import 'bold.dart';

/// Feature card: big illustration, a number, a chunky rounded headline and one short line.
class FeatureCard extends StatelessWidget {
  const FeatureCard({
    super.key,
    required this.slot,
    required this.number,
    required this.title,
    required this.message,
    this.width = 220,
  });

  final IllustrationSlot slot;
  final int number;
  final String title;
  final String message;
  final double width;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ill = illustratedColors(context);
    return SizedBox(
      width: width,
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(FreshSpacing.s4),
          child: MergeSemantics(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Center(child: Illustration(slot: slot, size: 120, excludeFromSemantics: true)),
                const SizedBox(height: FreshSpacing.s2),
                ExcludeSemantics(
                  child: Text(
                    number.toString().padLeft(2, '0'),
                    style: theme.textTheme.headlineMedium?.copyWith(color: ill.tealText, height: 1),
                  ),
                ),
                Text(title, style: theme.textTheme.titleLarge),
                const SizedBox(height: FreshSpacing.s1),
                Text(message, style: theme.textTheme.bodyMedium?.copyWith(color: freshColors(context).textMuted)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// "How can we help you today?" next to the support agent in the ringed circle (D88).
class HelpHero extends StatelessWidget {
  const HelpHero({super.key, this.title = 'How can we help you today?', this.message});

  final String title;
  final String? message;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ill = illustratedColors(context);
    return Container(
      padding: const EdgeInsets.all(FreshSpacing.s5),
      decoration: BoxDecoration(color: ill.tealSoft, borderRadius: BorderRadius.circular(AppRadius.hero)),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Semantics(header: true, child: Text(title, style: theme.textTheme.headlineMedium)),
                if (message != null) ...[
                  const SizedBox(height: FreshSpacing.s2),
                  Text(message!, style: theme.textTheme.bodyMedium),
                ],
              ],
            ),
          ),
          const SizedBox(width: FreshSpacing.s3),
          const EntranceMotion(child: AvatarCircle(slot: IllustrationSlot.helpSupport, size: 124)),
        ],
      ),
    );
  }
}
