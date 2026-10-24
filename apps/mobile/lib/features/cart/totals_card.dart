// Server totals (docs/api §5.2), rendered as they are: the "Bill details" box (D88). Used by Cart and Checkout.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../../api/models/cart.dart';
import '../../core/format.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/states.dart';

class TotalsCard extends StatelessWidget {
  const TotalsCard({super.key, required this.totals, this.title = 'Bill details'});

  final CartTotals totals;
  final String title;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = illustratedColors(context);
    final total = AppText.price(c.ink, size: 17);
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
              'Estimated total',
              formatRupees(totals.estimatedTotalPaise),
              style: total,
              key: const Key('estimatedTotal'),
            ),
            row('You pay at most', formatRupees(totals.maxTotalPaise), key: const Key('maxTotal')),
            if (totals.taxPaise != 0)
              Text(
                'Prices include ${formatRupees(totals.taxPaise)} GST.',
                style: theme.textTheme.bodySmall?.copyWith(color: c.muted),
              ),
            const SizedBox(height: FreshSpacing.s1),
            Text(
              'The final amount depends on the weighed net weight and is never more than the maximum.',
              style: theme.textTheme.bodySmall?.copyWith(color: c.muted),
            ),
          ],
        ),
      ),
    );
  }
}

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
