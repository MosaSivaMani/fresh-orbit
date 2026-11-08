// Server totals (docs/api §5.2), rendered as they are: the "Bill details" box (D88). Used by Cart and Checkout.
// D347: the price you see is the most you pay. The server sends max == estimated (no over-weight charge), so the
// total is shown plainly; only if an operator configures an over-weight allowance (max > estimated, two server
// numbers compared, nothing computed) does the "You pay at most" row appear.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../../api/models/cart.dart';
import '../../core/format.dart';

class TotalsCard extends StatelessWidget {
  const TotalsCard({super.key, required this.totals, this.title = 'Bill details', this.hasWeighedItems = false});

  final CartTotals totals;
  final String title;

  /// True when a line is sold by weight: adds the net-weight note under the total.
  final bool hasWeighedItems;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = illustratedColors(context);
    final total = AppText.price(c.ink, size: 17);
    final hasAllowance = totals.maxTotalPaise > totals.estimatedTotalPaise;
    Widget row(String label, String value, {TextStyle? style, Key? key}) => Padding(
      padding: const EdgeInsets.symmetric(vertical: FreshSpacing.s1),
      child: MergeSemantics(
        child: Row(
          children: [
            Expanded(child: Text(label, style: style ?? theme.textTheme.bodyMedium)),
            Text(value, key: key, style: style ?? theme.textTheme.bodyMedium),
          ],
        ),
      ),
    );
    return SectionBox(
      child: Padding(
        padding: const EdgeInsets.all(FreshSpacing.s1),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(header: true, child: SectionTitle(title)),
            const SizedBox(height: FreshSpacing.s2),
            row('Items', formatRupees(totals.itemsPaise)),
            if (totals.preparationPaise != 0) row('Cleaning and cutting', formatRupees(totals.preparationPaise)),
            if (totals.discountPaise != 0) row('Discount', '−${formatRupees(totals.discountPaise)}'),
            row(
              'Delivery fee',
              totals.deliveryFeeWaived || totals.deliveryFeePaise == 0 ? 'Free' : formatRupees(totals.deliveryFeePaise),
            ),
            Divider(height: FreshSpacing.s5, color: c.line),
            row(
              hasAllowance ? 'Estimated total' : 'Total',
              formatRupees(totals.estimatedTotalPaise),
              style: total,
              key: const Key('estimatedTotal'),
            ),
            if (hasAllowance) row('You pay at most', formatRupees(totals.maxTotalPaise), key: const Key('maxTotal')),
            if (totals.taxPaise != 0)
              Text(
                'Prices include ${formatRupees(totals.taxPaise)} GST.',
                style: theme.textTheme.bodySmall?.copyWith(color: c.muted),
              ),
            if (hasAllowance) ...[
              const SizedBox(height: FreshSpacing.s1),
              Text(
                'The final amount depends on the weighed net weight and is never more than the maximum.',
                key: const Key('weightNote'),
                style: theme.textTheme.bodySmall?.copyWith(color: c.muted),
              ),
            ] else if (hasWeighedItems) ...[
              const SizedBox(height: FreshSpacing.s1),
              Text(
                weighedItemsNote,
                key: const Key('weightNote'),
                style: theme.textTheme.bodySmall?.copyWith(color: c.muted),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// The plain note shown wherever weighed items are priced (D347): never more, only less.
const weighedItemsNote = 'Priced on net weight after cleaning. If your pack weighs less, you pay less — never more.';

/// True when a line is sold by weight (its quantity is in grams), so the net-weight note applies.
bool anyWeighed(Iterable<CartLine> lines) => lines.any((l) => l.quantity.unit == 'GRAM');

/// Minimum order and free-delivery shortfalls, as sent by the server.
class ShortfallBanners extends StatelessWidget {
  const ShortfallBanners({super.key, required this.totals});

  final CartTotals totals;

  @override
  Widget build(BuildContext context) {
    final min = totals.minOrderShortfallPaise ?? 0;
    final free = totals.freeDeliveryShortfallPaise ?? 0;
    return Column(
      children: [
        if (min > 0)
          Padding(
            padding: const EdgeInsets.only(bottom: FreshSpacing.s2),
            child: InfoBanner(
              tone: BannerTone.warning,
              text:
                  'Add ${formatRupees(min)} more to place an order'
                  '${totals.minOrderPaise == null ? '' : ' (minimum ${formatRupees(totals.minOrderPaise!)})'}.',
            ),
          ),
        if (free > 0)
          Padding(
            padding: const EdgeInsets.only(bottom: FreshSpacing.s2),
            child: InfoBanner(text: 'Add ${formatRupees(free)} more for free delivery.'),
          ),
      ],
    );
  }
}
