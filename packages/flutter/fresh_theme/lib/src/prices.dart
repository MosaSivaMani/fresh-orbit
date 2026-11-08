// Price rows. Every amount arrives already formatted from a server value (rule 4); these parts only lay it out.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// The server's price for one line before it is added: "₹649 for 500 g" in the price face and, only when an
/// over-weight allowance is configured (D347 default: none), the most it can cost after weighing under it
/// ([atMost], null otherwise). A soft teal tile on the grid's tile radius.
class PriceEstimateRow extends StatelessWidget {
  const PriceEstimateRow({super.key, required this.estimate, this.atMost});

  /// For example "₹649 for 500 g" (or "Estimated ₹649 for 500 g" when [atMost] is given).
  final String estimate;

  /// For example "You pay at most ₹715 (up to 550 g)." Null when the maximum equals the price.
  final String? atMost;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: FreshLayout.cardGap, vertical: 10),
      decoration: BoxDecoration(color: c.tealSoft, borderRadius: BorderRadius.circular(FreshLayout.tileRadius)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(estimate, style: AppText.price(c.tealDeep, size: 16)),
          if (atMost != null)
            Text(
              atMost!,
              style: TextStyle(fontFamily: AppFonts.body, fontSize: 12, height: 1.3, color: c.ink),
            ),
        ],
      ),
    );
  }
}

/// What one fixed pack holds (D348), already formatted from the server's values: "500 g net · 5–7 pieces", with
/// the price when the screen gives it ("500 g net · 5–7 pieces · ₹749"), and a small [caption] that says net
/// weight is the weight after cleaning. [showCaption] false keeps the caption for screen readers only (a dense
/// cart row). Gross weight is never shown.
class FreshPackFacts extends StatelessWidget {
  const FreshPackFacts({super.key, required this.facts, this.caption, this.showCaption = true, this.dense = false});

  /// For example "500 g net · 5–7 pieces · ₹749".
  final String facts;

  /// For example "Net weight is the weight after cleaning."
  final String? caption;
  final bool showCaption;

  /// A smaller line without the scale icon, for a cart row.
  final bool dense;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final factsText = Text(
      facts,
      key: const ValueKey('packFactsText'),
      style: TextStyle(
        fontFamily: AppFonts.body,
        fontSize: dense ? 12 : 14,
        height: 1.3,
        fontWeight: FontWeight.w700,
        color: dense ? c.ink : c.tealDeep,
      ),
    );
    return Semantics(
      label: caption == null ? facts : '$facts. $caption',
      excludeSemantics: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (dense)
            factsText
          else
            Row(
              children: [
                Icon(Icons.scale_outlined, size: 16, color: c.tealDeep),
                const SizedBox(width: 6),
                Flexible(child: factsText),
              ],
            ),
          if (caption != null && showCaption)
            Text(
              caption!,
              style: TextStyle(fontFamily: AppFonts.body, fontSize: 11.5, height: 1.3, color: c.muted),
            ),
        ],
      ),
    );
  }
}

/// A coupon the customer may use: its code on a dashed-looking pill, the owner's description and the minimum
/// order, with one action (Apply). Values arrive formatted.
class CouponTile extends StatelessWidget {
  const CouponTile({
    super.key,
    required this.code,
    this.description,
    this.condition,
    required this.actionLabel,
    this.onAction,
    this.busy = false,
  });

  final String code;
  final String? description;

  /// For example "On orders of ₹499 or more".
  final String? condition;
  final String actionLabel;
  final VoidCallback? onAction;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return FreshCard(
      color: c.paper,
      shadow: cardShadow(context),
      radius: FreshLayout.tileRadius,
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: c.mangoSoft,
                    borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
                    border: Border.all(color: c.mango, width: 1.5),
                  ),
                  child: Text(code, style: AppText.price(c.ink, size: 14)),
                ),
                if (description != null) ...[
                  const SizedBox(height: 6),
                  Text(description!, style: TextStyle(fontFamily: AppFonts.body, fontSize: 13, color: c.ink)),
                ],
                if (condition != null)
                  Text(condition!, style: TextStyle(fontFamily: AppFonts.body, fontSize: 12, color: c.muted)),
              ],
            ),
          ),
          const SizedBox(width: FreshLayout.cardGap),
          busy
              ? const SizedBox.square(dimension: 24, child: CircularProgressIndicator(strokeWidth: 2.5))
              : TextButton(onPressed: onAction, child: Text(actionLabel)),
        ],
      ),
    );
  }
}
