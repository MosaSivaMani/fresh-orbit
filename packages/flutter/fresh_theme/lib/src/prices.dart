// Price rows. Every amount arrives already formatted from a server value (rule 4); these parts only lay it out.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// The server's estimate for one line before it is added: "Estimated ₹649 for 500 g" in the price face and,
/// under it, the most it can cost after weighing. A soft teal tile on the grid's tile radius.
class PriceEstimateRow extends StatelessWidget {
  const PriceEstimateRow({super.key, required this.estimate, this.atMost});

  /// For example "Estimated ₹649 for 500 g".
  final String estimate;

  /// For example "You pay at most ₹715 (up to 550 g)."
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
