// Commerce widgets: price tag, quantity stepper, availability and status badges, product image and card.
// They only format server values (docs/design §6 rule 1).

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

import '../api/models/catalog.dart';
import '../api/models/common.dart';
import '../core/format.dart';
import 'bold.dart';

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
          borderRadius: BorderRadius.circular(FreshRadius.full),
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

/// A cut's charge at the pincode, as the server gives it: "Free", "+₹40/kg", or "Not available here" when the
/// server has no charge for it (not offered at this location).
String preparationChargeText(Preparation p) {
  final c = p.chargePaise;
  if (c == null) return 'Not available here';
  if (c == 0) return 'Free';
  return switch (p.chargeBasis) {
    'PER_KG' => '+${formatRupees(c)}/kg',
    'PER_ITEM' => '+${formatRupees(c)}/piece',
    _ => '+${formatRupees(c)}',
  };
}

/// What the picture-edge button of a [ProductCard] shows.
sealed class CardCartControl {
  const CardCartControl();
}

/// "ADD": adds the product (null [onAdd] shows it disabled).
class CardAdd extends CardCartControl {
  const CardAdd(this.onAdd);

  final VoidCallback? onAdd;
}

/// "− 500 g +" for the one cart line of this product; [label] is the server's quantity, formatted.
class CardStepper extends CardCartControl {
  const CardStepper({required this.label, required this.onDecrease, required this.onIncrease});

  final String label;
  final VoidCallback onDecrease;
  final VoidCallback onIncrease;
}

/// "2 IN CART": the product has more than one line (different cuts); tapping opens the cart.
class CardInCart extends CardCartControl {
  const CardInCart({required this.lines, required this.onOpen});

  final int lines;
  final VoidCallback onOpen;
}

/// Shop product card (D88): a tinted picture with an availability chip, an ADD button on the picture's bottom
/// edge that turns into a − n + stepper once the product is in the cart, then the name, local names and the
/// server's price per unit with the struck compare-at price. It shows server values only; it never works out a
/// discount (rule 4).
class ProductCard extends StatelessWidget {
  const ProductCard({
    super.key,
    required this.product,
    required this.onTap,
    this.control,
    this.pending = false,
    this.tint,
  });

  final ProductSummary product;
  final VoidCallback onTap;

  /// Null hides the picture-edge button.
  final CardCartControl? control;
  final bool pending;

  /// Picture background; defaults to teal-soft.
  final Color? tint;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final ill = illustratedColors(context);
    final price = product.fromPrice;
    final availability = product.availability;
    final soldOut = availability != null && !availability.canAdd;
    final chipLabel = switch (availability?.status) {
      null || 'IN_STOCK' => null,
      'LOW_STOCK' => 'Low stock',
      'OUT_OF_STOCK' => 'Sold out',
      'NOT_SOLD_HERE' => 'Not sold here',
      final s => humanizeCode(s),
    };
    const edge = 17.0;
    return Container(
      decoration: BoxDecoration(
        color: ill.paper,
        borderRadius: BorderRadius.circular(FreshLayout.cardRadius),
        boxShadow: cardShadow(context),
      ),
      child: Material(
        type: MaterialType.transparency,
        child: InkWell(
          borderRadius: BorderRadius.circular(FreshLayout.cardRadius),
          onTap: onTap,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Stack(
                children: [
                  Padding(
                    padding: const EdgeInsets.only(bottom: edge),
                    child: Opacity(
                      opacity: soldOut ? 0.55 : 1,
                      child: ClipRRect(
                        borderRadius: const BorderRadius.vertical(top: Radius.circular(FreshLayout.cardRadius)),
                        child: ColoredBox(
                          color: tint ?? ill.tealSoft,
                          // The 3:2 picture frame of every card (D193).
                          child: ProductPhoto(
                            url: product.imageUrl,
                            aspectRatio: FreshLayout.pictureAspect,
                            radius: 0,
                            transparent: true,
                          ),
                        ),
                      ),
                    ),
                  ),
                  if (chipLabel != null)
                    Positioned(
                      top: FreshSpacing.s2,
                      left: FreshSpacing.s2,
                      child: _FreshChip(label: chipLabel),
                    ),
                  if (control != null)
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: 0,
                      child: Center(
                        child: _CardControl(control: control!, pending: pending, name: product.name),
                      ),
                    ),
                ],
              ),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(FreshLayout.cardGap, 6, FreshLayout.cardGap, FreshLayout.cardGap),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        product.name,
                        style: AppText.display(ill.ink, 14),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                      if (product.localNames.isNotEmpty)
                        Text(
                          product.localNames.join(' · '),
                          style: theme.textTheme.bodySmall?.copyWith(color: ill.muted, fontSize: 11),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      const Spacer(),
                      if (price != null)
                        PriceTag(
                          paise: price.unitPricePaise,
                          compareAtPaise: price.compareAtPaise,
                          perUnit: price.perUnit,
                          style: AppText.price(ill.ink),
                        )
                      else if (product.locationRequired)
                        Text('Set pincode to see prices', style: theme.textTheme.bodySmall),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FreshChip extends StatelessWidget {
  const _FreshChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
    decoration: BoxDecoration(color: BlockInk.paper, borderRadius: BorderRadius.circular(999)),
    child: Text(
      label,
      style: const TextStyle(fontFamily: AppFonts.body, fontSize: 10, fontWeight: FontWeight.w700, color: BlockInk.ink),
    ),
  );
}

class _CardControl extends StatelessWidget {
  const _CardControl({required this.control, required this.pending, required this.name});

  final CardCartControl control;
  final bool pending;
  final String name;

  @override
  Widget build(BuildContext context) {
    final ill = illustratedColors(context);
    // Its own node, so the card's tap and the ADD are separate for screen readers.
    Widget pill(String text, String semantics, VoidCallback? onTap) => Semantics(
      container: true,
      button: true,
      enabled: onTap != null,
      label: semantics,
      excludeSemantics: true,
      child: Material(
        color: ill.paper,
        elevation: 3,
        shadowColor: ill.softShadow,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(10),
          side: BorderSide(color: onTap == null ? ill.line : ill.tealText, width: 1.5),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(10),
          onTap: pending ? null : onTap,
          child: Container(
            constraints: const BoxConstraints(minWidth: 88, minHeight: 34),
            alignment: Alignment.center,
            padding: const EdgeInsets.symmetric(horizontal: 14),
            child: pending
                ? SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2, color: ill.tealText, semanticsLabel: 'Adding'),
                  )
                : Text(
                    text,
                    style: TextStyle(
                      fontFamily: AppFonts.body,
                      fontSize: 12.5,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 0.5,
                      color: onTap == null ? ill.muted : ill.tealDeep,
                    ),
                  ),
          ),
        ),
      ),
    );
    return switch (control) {
      CardAdd(:final onAdd) => pill('ADD', 'Add $name to cart', onAdd),
      CardInCart(:final lines, :final onOpen) => pill(
        '$lines IN CART',
        '$lines lines of $name in cart, open cart',
        onOpen,
      ),
      CardStepper(:final label, :final onDecrease, :final onIncrease) => Material(
        color: Colors.transparent,
        elevation: 3,
        shadowColor: ill.softShadow,
        borderRadius: BorderRadius.circular(10),
        child: CompactStepper(
          label: label,
          itemName: name,
          filled: true,
          pending: pending,
          onDecrease: onDecrease,
          onIncrease: onIncrease,
        ),
      ),
    };
  }
}
