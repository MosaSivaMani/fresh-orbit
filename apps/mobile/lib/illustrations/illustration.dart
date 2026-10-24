// Illustration(slot:, size:) shows `assets/illustrations/<slot_lower_snake>.webp` when the app bundles it, and
// otherwise our own placeholder: a soft sand circle with one simple shape per slot, drawn in code. The images
// contain no text, so the widget never needs translating.

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../theme/app_theme.dart';
import 'illustration_slot.dart';

export 'illustration_slot.dart';

/// Which illustration files the app bundle holds, read once from the asset manifest.
abstract final class IllustrationAssets {
  static final ValueNotifier<Set<String>?> available = ValueNotifier<Set<String>?>(null);
  static Future<void>? _loading;

  static void ensureLoaded(AssetBundle bundle) {
    if (available.value != null || _loading != null) return;
    _loading = AssetManifest.loadFromAssetBundle(bundle).then(
      (manifest) => available.value = manifest
          .listAssets()
          .where((a) => a.startsWith('assets/illustrations/') && a.endsWith('.webp'))
          .toSet(),
      onError: (Object _) => available.value = const <String>{},
    );
  }

  /// Tests: pretend the bundle holds exactly [assets] (null forgets the answer and reads the manifest again).
  @visibleForTesting
  static void debugSetAvailable(Set<String>? assets) {
    _loading = null;
    available.value = assets;
  }
}

class Illustration extends StatefulWidget {
  const Illustration({
    super.key,
    required this.slot,
    this.size = 160,
    this.semanticLabel,
    this.excludeFromSemantics = false,
  });

  final IllustrationSlot slot;

  /// Width and height in logical pixels (illustrations are square).
  final double size;

  /// Overrides the slot's default alt text.
  final String? semanticLabel;

  /// Set when the text next to the illustration already says the same thing.
  final bool excludeFromSemantics;

  @override
  State<Illustration> createState() => _IllustrationState();
}

class _IllustrationState extends State<Illustration> {
  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    IllustrationAssets.ensureLoaded(DefaultAssetBundle.of(context));
  }

  @override
  Widget build(BuildContext context) {
    final placeholder = IllustrationPlaceholder(slot: widget.slot, size: widget.size);
    final image = ValueListenableBuilder<Set<String>?>(
      valueListenable: IllustrationAssets.available,
      builder: (context, assets, _) {
        if (assets == null || !assets.contains(widget.slot.assetPath)) return placeholder;
        return Image.asset(
          widget.slot.assetPath,
          key: ValueKey('illustration-image-${widget.slot.code}'),
          width: widget.size,
          height: widget.size,
          fit: BoxFit.contain,
          excludeFromSemantics: true,
          errorBuilder: (_, _, _) => placeholder,
        );
      },
    );
    final box = SizedBox.square(dimension: widget.size, child: image);
    if (widget.excludeFromSemantics) return ExcludeSemantics(child: box);
    return Semantics(
      image: true,
      label: widget.semanticLabel ?? widget.slot.semanticLabel,
      child: ExcludeSemantics(child: box),
    );
  }
}

/// Our own stand-in until the final image ships: sand circle, two bubbles, a wave and one shape for the slot.
class IllustrationPlaceholder extends StatelessWidget {
  const IllustrationPlaceholder({super.key, required this.slot, required this.size});

  final IllustrationSlot slot;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return SizedBox.square(
      key: ValueKey('illustration-placeholder-${slot.code}'),
      dimension: size,
      child: CustomPaint(
        painter: _PlaceholderPainter(colors: c, fish: _fishSlots.contains(slot)),
        child: Center(
          child: Padding(
            padding: EdgeInsets.only(bottom: size * 0.04),
            child: Icon(_iconFor(slot), size: size * 0.38, color: c.tealText),
          ),
        ),
      ),
    );
  }
}

/// Slots whose placeholder also gets the small pomfret sidekick.
const _fishSlots = {
  IllustrationSlot.onboardingFresh,
  IllustrationSlot.emptyCart,
  IllustrationSlot.emptyWishlist,
  IllustrationSlot.emptyNotifications,
  IllustrationSlot.noSearchResults,
  IllustrationSlot.errorGeneric,
  IllustrationSlot.notFound,
  IllustrationSlot.homeHero,
  IllustrationSlot.featureHonestWeight,
};

IconData _iconFor(IllustrationSlot slot) => switch (slot) {
  IllustrationSlot.onboardingFresh => Icons.set_meal_rounded,
  IllustrationSlot.onboardingCleaned => Icons.content_cut_rounded,
  IllustrationSlot.onboardingDelivery => Icons.delivery_dining_rounded,
  IllustrationSlot.locationPick => Icons.location_on_rounded,
  IllustrationSlot.notServiceable => Icons.wrong_location_rounded,
  IllustrationSlot.emptyCart => Icons.shopping_basket_rounded,
  IllustrationSlot.emptyWishlist => Icons.favorite_rounded,
  IllustrationSlot.emptyOrders => Icons.receipt_long_rounded,
  IllustrationSlot.emptyNotifications => Icons.notifications_none_rounded,
  IllustrationSlot.emptyAddresses => Icons.home_rounded,
  IllustrationSlot.noSearchResults => Icons.search_off_rounded,
  IllustrationSlot.outOfStock => Icons.inventory_2_rounded,
  IllustrationSlot.loginOtp => Icons.sms_rounded,
  IllustrationSlot.orderPlaced => Icons.check_circle_rounded,
  IllustrationSlot.orderPacking => Icons.inventory_rounded,
  IllustrationSlot.orderOnTheWay => Icons.delivery_dining_rounded,
  IllustrationSlot.orderDelivered => Icons.where_to_vote_rounded,
  IllustrationSlot.orderCancelled => Icons.cancel_rounded,
  IllustrationSlot.complaintReceived => Icons.assignment_turned_in_rounded,
  IllustrationSlot.helpSupport => Icons.support_agent_rounded,
  IllustrationSlot.errorGeneric => Icons.error_outline_rounded,
  IllustrationSlot.offline => Icons.wifi_off_rounded,
  IllustrationSlot.notFound => Icons.explore_off_rounded,
  IllustrationSlot.homeHero => Icons.set_meal_rounded,
  IllustrationSlot.featureHonestWeight => Icons.scale_rounded,
  IllustrationSlot.featureCleanedYourWay => Icons.restaurant_rounded,
  IllustrationSlot.featureColdChain => Icons.ac_unit_rounded,
  IllustrationSlot.featureSameDay => Icons.schedule_rounded,
};

class _PlaceholderPainter extends CustomPainter {
  _PlaceholderPainter({required this.colors, required this.fish});

  final IllustratedColors colors;
  final bool fish;

  @override
  void paint(Canvas canvas, Size size) {
    final s = size.shortestSide;
    final center = Offset(size.width / 2, size.height / 2);
    final r = s * 0.44;

    // Soft sand circle with a lighter top-left, like a clay ball lit from above.
    final circle = Paint()
      ..shader = RadialGradient(
        center: const Alignment(-0.35, -0.45),
        radius: 1.05,
        colors: [colors.paper, colors.sandDeep],
      ).createShader(Rect.fromCircle(center: center, radius: r));
    canvas.drawCircle(center + Offset(0, s * 0.025), r, Paint()..color = colors.softShadow);
    canvas.drawCircle(center, r, circle);

    // A small wave across the lower part of the circle.
    final wave = Path()..moveTo(center.dx - r * 0.7, center.dy + r * 0.55);
    for (var i = 0; i < 4; i++) {
      final x = center.dx - r * 0.7 + i * r * 0.35;
      wave.quadraticBezierTo(x + r * 0.0875, center.dy + r * 0.45, x + r * 0.175, center.dy + r * 0.55);
      wave.quadraticBezierTo(x + r * 0.2625, center.dy + r * 0.65, x + r * 0.35, center.dy + r * 0.55);
    }
    canvas.drawPath(
      wave,
      Paint()
        ..color = colors.tealDecor.withValues(alpha: 0.35)
        ..style = PaintingStyle.stroke
        ..strokeWidth = math.max(1.5, s * 0.018)
        ..strokeCap = StrokeCap.round,
    );

    // Bubbles: coral and teal.
    canvas.drawCircle(center + Offset(r * 0.78, -r * 0.72), s * 0.045, Paint()..color = colors.coral);
    canvas.drawCircle(center + Offset(-r * 0.88, -r * 0.35), s * 0.028, Paint()..color = colors.tealDecor);
    canvas.drawCircle(center + Offset(r * 0.95, -r * 0.35), s * 0.02, Paint()..color = colors.tealDecor);

    if (fish) _paintPomfret(canvas, center + Offset(r * 0.62, r * 0.62), s * 0.16);
  }

  /// The pomfret sidekick: a rounded diamond body, a tail and an eye.
  void _paintPomfret(Canvas canvas, Offset c, double w) {
    final body = Path()
      ..moveTo(c.dx - w * 0.5, c.dy)
      ..quadraticBezierTo(c.dx - w * 0.1, c.dy - w * 0.55, c.dx + w * 0.35, c.dy)
      ..quadraticBezierTo(c.dx - w * 0.1, c.dy + w * 0.55, c.dx - w * 0.5, c.dy)
      ..close();
    final tail = Path()
      ..moveTo(c.dx + w * 0.3, c.dy)
      ..lineTo(c.dx + w * 0.6, c.dy - w * 0.25)
      ..lineTo(c.dx + w * 0.6, c.dy + w * 0.25)
      ..close();
    final fill = Paint()..color = colors.coral;
    final outline = Paint()
      ..color = colors.ink
      ..style = PaintingStyle.stroke
      ..strokeWidth = math.max(1, w * 0.07)
      ..strokeJoin = StrokeJoin.round;
    canvas
      ..drawPath(tail, fill)
      ..drawPath(tail, outline)
      ..drawPath(body, fill)
      ..drawPath(body, outline)
      ..drawCircle(Offset(c.dx - w * 0.25, c.dy - w * 0.05), w * 0.06, Paint()..color = colors.ink);
  }

  @override
  bool shouldRepaint(_PlaceholderPainter old) => old.colors != colors || old.fish != fish;
}
