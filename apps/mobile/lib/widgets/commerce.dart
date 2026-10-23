// Commerce widgets: price tag, quantity stepper, availability and status badges, product image and card.
// They only format server values (docs/design §6 rule 1).

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../api/models/catalog.dart';
import '../api/models/common.dart';
import '../core/format.dart';
import 'states.dart';

/// Paise in, ₹ out: price, optional struck-through compare-at price ("Original price"), optional unit.
class PriceTag extends StatelessWidget {
  const PriceTag({super.key, required this.paise, this.compareAtPaise, this.perUnit, this.prefix, this.style});

  final int paise;
  final int? compareAtPaise;
  final String? perUnit;
  final String? prefix;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final main = style ?? theme.textTheme.titleMedium;
    final unit = perUnit == null ? '' : formatPerUnit(perUnit!);
    final price = formatRupees(paise);
    final compare = compareAtPaise != null && compareAtPaise! > paise ? formatRupees(compareAtPaise!) : null;
    final label = [
      if (prefix != null) prefix,
      '$price$unit',
      if (compare != null) 'Original price $compare',
    ].join(', ');
    return Semantics(
      label: label,
      excludeSemantics: true,
      child: Wrap(
        crossAxisAlignment: WrapCrossAlignment.end,
        spacing: FreshSpacing.s1,
        children: [
          if (prefix != null) Text(prefix!, style: theme.textTheme.bodySmall),
          Text('$price$unit', style: main),
          if (compare != null)
            Text(
              compare,
              style: theme.textTheme.bodySmall?.copyWith(
                decoration: TextDecoration.lineThrough,
                color: freshColors(context).textMuted,
              ),
            ),
        ],
      ),
    );
  }
}

/// Proposes a new quantity using the server's min / max / step; the cart or product screen sends it and shows
/// the server's answer. Buttons are 48 dp and named for screen readers; the value is a live region.
class QuantityStepper extends StatelessWidget {
  const QuantityStepper({
    super.key,
    required this.value,
    required this.min,
    required this.max,
    required this.step,
    required this.itemName,
    required this.onChanged,
    this.pending = false,
    this.allowRemove = false,
  });

  final Quantity value;
  final Quantity min;
  final Quantity max;
  final Quantity step;
  final String itemName;

  /// Called with the proposed quantity, or null when [allowRemove] and decreasing below min.
  final ValueChanged<Quantity?> onChanged;
  final bool pending;
  final bool allowRemove;

  @override
  Widget build(BuildContext context) {
    final canDecrease = !pending && (value.value - step.value >= min.value || allowRemove);
    final canIncrease = !pending && value.value + step.value <= max.value;
    final removes = value.value - step.value < min.value;
    return Semantics(
      container: true,
      label: 'Quantity of $itemName',
      child: Container(
        decoration: BoxDecoration(
          border: Border.all(color: freshColors(context).borderStrong),
          borderRadius: BorderRadius.circular(FreshRadius.md),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            IconButton(
              tooltip: removes && allowRemove ? 'Remove $itemName' : 'Decrease quantity of $itemName',
              icon: Icon(removes && allowRemove ? Icons.delete_outline : Icons.remove),
              constraints: const BoxConstraints(minWidth: 48, minHeight: 48),
              onPressed: canDecrease
                  ? () => onChanged(removes ? null : value.withValue(value.value - step.value))
                  : null,
            ),
            ConstrainedBox(
              constraints: const BoxConstraints(minWidth: 64),
              child: Semantics(
                liveRegion: true,
                child: pending
                    ? const Center(
                        child: SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2, semanticsLabel: 'Updating'),
                        ),
                      )
                    : Text(
                        formatQuantity(value),
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
              ),
            ),
            IconButton(
              tooltip: 'Increase quantity of $itemName',
              icon: const Icon(Icons.add),
              constraints: const BoxConstraints(minWidth: 48, minHeight: 48),
              onPressed: canIncrease ? () => onChanged(value.withValue(value.value + step.value)) : null,
            ),
          ],
        ),
      ),
    );
  }
}

/// Small text badge. Tone is decoration; the text carries the meaning.
class FreshBadge extends StatelessWidget {
  const FreshBadge({super.key, required this.label, this.tone = BannerTone.info, this.highlight = false});

  final String label;
  final BannerTone tone;

  /// Warm sand (offers, bestsellers).
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    final c = freshColors(context);
    final (bg, fg) = highlight
        ? (c.highlightSubtle, c.highlightText)
        : switch (tone) {
            BannerTone.info => (c.infoSubtle, c.infoText),
            BannerTone.warning => (c.warningSubtle, c.warningText),
            BannerTone.danger => (c.dangerSubtle, c.dangerText),
            BannerTone.success => (c.successSubtle, c.successText),
          };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s2, vertical: FreshSpacing.s0_5),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(FreshRadius.full)),
      child: Text(label, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: fg)),
    );
  }
}

/// Availability status → badge (server message wins when present).
class AvailabilityBadge extends StatelessWidget {
  const AvailabilityBadge({super.key, required this.status, this.message});

  final String status;
  final String? message;

  @override
  Widget build(BuildContext context) {
    final (label, tone) = switch (status) {
      'IN_STOCK' => ('In stock', BannerTone.success),
      'LOW_STOCK' => ('Low stock', BannerTone.warning),
      'OUT_OF_STOCK' => ('Sold out', BannerTone.danger),
      'NOT_SOLD_HERE' => ('Not sold here', BannerTone.danger),
      _ => (humanizeCode(status), BannerTone.info),
    };
    return FreshBadge(label: message ?? label, tone: tone);
  }
}

/// Order status labels (UI copy; codes come from the API, docs/api §6). Unknown codes fall back to humanised.
const orderStatusLabels = <String, String>{
  'PENDING_PAYMENT': 'Awaiting payment',
  'CONFIRMED': 'Confirmed',
  'PREPARING': 'Being prepared',
  'PACKED': 'Packed',
  'OUT_FOR_DELIVERY': 'Out for delivery',
  'DELIVERED': 'Delivered',
  'DELIVERY_FAILED': 'Delivery attempt failed',
  'RETURNED': 'Returned',
  'CANCELLED': 'Cancelled',
  'PAYMENT_FAILED': 'Payment failed',
};

String orderStatusLabel(String code) => orderStatusLabels[code] ?? humanizeCode(code);

class OrderStatusBadge extends StatelessWidget {
  const OrderStatusBadge({super.key, required this.status});

  final String status;

  @override
  Widget build(BuildContext context) {
    final tone = switch (status) {
      'DELIVERED' => BannerTone.success,
      'CANCELLED' || 'PAYMENT_FAILED' || 'RETURNED' || 'DELIVERY_FAILED' => BannerTone.danger,
      'PENDING_PAYMENT' => BannerTone.warning,
      _ => BannerTone.info,
    };
    return FreshBadge(label: orderStatusLabel(status), tone: tone);
  }
}

const paymentMethodLabels = <String, String>{
  'COD': 'Cash on delivery',
  'UPI_ON_DELIVERY': 'UPI on delivery',
  'ONLINE': 'Pay online',
};

String paymentMethodLabel(String code) => paymentMethodLabels[code] ?? humanizeCode(code);

/// Network image with a calm placeholder; decorative unless [semanticLabel] is given.
class ProductPhoto extends StatelessWidget {
  const ProductPhoto({
    super.key,
    required this.url,
    this.semanticLabel,
    this.aspectRatio = 1,
    this.radius = FreshRadius.lg,
  });

  final String? url;
  final String? semanticLabel;
  final double aspectRatio;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final placeholder = Container(
      color: freshColors(context).muted,
      alignment: Alignment.center,
      child: Icon(Icons.set_meal_outlined, color: freshColors(context).textMuted, size: 40),
    );
    return AspectRatio(
      aspectRatio: aspectRatio,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(radius),
        child: url == null
            ? ExcludeSemantics(child: placeholder)
            : Image.network(
                url!,
                fit: BoxFit.cover,
                semanticLabel: semanticLabel,
                excludeFromSemantics: semanticLabel == null,
                errorBuilder: (_, _, _) => placeholder,
                loadingBuilder: (context, child, progress) => progress == null ? child : placeholder,
              ),
      ),
    );
  }
}

/// Product card for grids: image, name, local names, server price per unit, availability.
class ProductCard extends StatelessWidget {
  const ProductCard({super.key, required this.product, required this.onTap});

  final ProductSummary product;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final price = product.fromPrice;
    final availability = product.availability;
    final soldOut = availability != null && !availability.canAdd;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Opacity(
          opacity: soldOut ? 0.6 : 1,
          child: Padding(
            padding: const EdgeInsets.all(FreshSpacing.s2),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ProductPhoto(url: product.imageUrl, aspectRatio: 1.1),
                const SizedBox(height: FreshSpacing.s2),
                Text(product.name, style: theme.textTheme.titleSmall, maxLines: 2, overflow: TextOverflow.ellipsis),
                if (product.localNames.isNotEmpty)
                  Text(
                    product.localNames.join(' · '),
                    style: theme.textTheme.bodySmall?.copyWith(color: freshColors(context).textMuted),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                const Spacer(),
                if (price != null)
                  PriceTag(
                    paise: price.unitPricePaise,
                    compareAtPaise: price.compareAtPaise,
                    perUnit: price.perUnit,
                    style: theme.textTheme.titleSmall,
                  )
                else if (product.locationRequired)
                  Text('Set pincode to see prices', style: theme.textTheme.bodySmall),
                if (availability != null && availability.status != 'IN_STOCK') ...[
                  const SizedBox(height: FreshSpacing.s1),
                  AvailabilityBadge(status: availability.status),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// A full-width primary button with a loading state that disables it (one action per tap).
class PrimaryButton extends StatelessWidget {
  const PrimaryButton({super.key, required this.label, required this.onPressed, this.loading = false, this.icon});

  final String label;
  final VoidCallback? onPressed;
  final bool loading;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final child = loading
        ? const SizedBox(
            width: 20,
            height: 20,
            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white, semanticsLabel: 'Working'),
          )
        : Text(label);
    return SizedBox(
      width: double.infinity,
      child: Semantics(
        button: true,
        label: loading ? '$label, working' : null,
        child: icon == null || loading
            ? FilledButton(onPressed: loading ? null : onPressed, child: child)
            : FilledButton.icon(onPressed: onPressed, icon: Icon(icon), label: child),
      ),
    );
  }
}
