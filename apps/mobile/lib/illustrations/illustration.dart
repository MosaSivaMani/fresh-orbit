// Illustration(slot:, size:) shows `assets/illustrations/<slot_lower_snake>.png` (or `.webp`, or a full-scene `.jpg`) when the app
// bundles it, and otherwise our own placeholder: one of the cast drawn in code on a soft clay circle, with a
// small prop badge for the slot. The images contain no text, so the widget never needs translating.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../theme/app_theme.dart';
import 'cast_painter.dart';
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
          .where(
            (a) =>
                a.startsWith('assets/illustrations/') &&
                (a.endsWith('.png') || a.endsWith('.webp') || a.endsWith('.jpg')),
          )
          .toSet(),
      onError: (Object _) => available.value = const <String>{},
    );
  }

  /// The first bundled file for [slot] (PNG, then WebP, then JPEG), or null when none is bundled.
  static String? pathFor(IllustrationSlot slot, Set<String> assets) {
    for (final path in slot.assetPaths) {
      if (assets.contains(path)) return path;
    }
    return null;
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
        final path = assets == null ? null : IllustrationAssets.pathFor(widget.slot, assets);
        if (path == null) return placeholder;
        // A JPEG is a full scene with its own background, so it fills a rounded frame instead of floating.
        final scene = path.endsWith('.jpg');
        final picture = Image.asset(
          path,
          key: ValueKey('illustration-image-${widget.slot.code}'),
          width: widget.size,
          height: widget.size,
          fit: scene ? BoxFit.cover : BoxFit.contain,
          excludeFromSemantics: true,
          errorBuilder: (_, _, _) => placeholder,
        );
        if (!scene) return picture;
        return ClipRRect(borderRadius: BorderRadius.circular(widget.size * 0.12), child: picture);
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

/// Our own stand-in until the final image ships: the slot's cast member on a clay circle, plus a prop badge.
class IllustrationPlaceholder extends StatelessWidget {
  const IllustrationPlaceholder({super.key, required this.slot, required this.size});

  final IllustrationSlot slot;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final (member, backdrop, prop, sidekick) = castFor(slot);
    final badge = size * 0.3;
    return SizedBox.square(
      key: ValueKey('illustration-placeholder-${slot.code}'),
      dimension: size,
      child: Stack(
        children: [
          Positioned.fill(
            child: CustomPaint(
              painter: CastPainter(member: member, colors: c, backdrop: backdrop, sidekick: sidekick),
            ),
          ),
          if (prop != null && size >= 64)
            Positioned(
              right: size * 0.04,
              bottom: size * 0.06,
              child: Container(
                width: badge,
                height: badge,
                decoration: BoxDecoration(
                  color: c.paper,
                  shape: BoxShape.circle,
                  border: Border.all(color: c.ink, width: badge * 0.06),
                  boxShadow: [BoxShadow(color: c.ink, offset: Offset(badge * 0.06, badge * 0.06))],
                ),
                alignment: Alignment.center,
                child: Icon(prop, size: badge * 0.58, color: c.tealText),
              ),
            ),
        ],
      ),
    );
  }
}

/// Who stands in each slot, on which tint, with which prop, and whether Chanduva joins them.
(CastMember, CastBackdrop, IconData?, bool) castFor(IllustrationSlot slot) => switch (slot) {
  IllustrationSlot.onboardingFresh => (CastMember.padma, CastBackdrop.teal, Icons.set_meal_rounded, true),
  IllustrationSlot.onboardingCleaned => (CastMember.padma, CastBackdrop.sand, Icons.content_cut_rounded, false),
  IllustrationSlot.onboardingDelivery => (CastMember.ravi, CastBackdrop.ice, Icons.ac_unit_rounded, false),
  IllustrationSlot.locationPick => (CastMember.ammamma, CastBackdrop.teal, Icons.location_on_rounded, false),
  IllustrationSlot.notServiceable => (CastMember.ravi, CastBackdrop.coral, Icons.wrong_location_rounded, false),
  IllustrationSlot.emptyCart => (CastMember.chanduva, CastBackdrop.sand, Icons.shopping_basket_rounded, false),
  IllustrationSlot.emptyWishlist => (CastMember.chanduva, CastBackdrop.coral, Icons.favorite_rounded, false),
  IllustrationSlot.emptyOrders => (CastMember.ammamma, CastBackdrop.sand, Icons.receipt_long_rounded, false),
  IllustrationSlot.emptyNotifications => (
    CastMember.chanduva,
    CastBackdrop.teal,
    Icons.notifications_none_rounded,
    false,
  ),
  IllustrationSlot.emptyAddresses => (CastMember.ammamma, CastBackdrop.sand, Icons.home_rounded, false),
  IllustrationSlot.noSearchResults => (CastMember.chanduva, CastBackdrop.ice, Icons.search_rounded, false),
  IllustrationSlot.outOfStock => (CastMember.padma, CastBackdrop.coral, Icons.inventory_2_rounded, false),
  IllustrationSlot.loginOtp => (CastMember.ammamma, CastBackdrop.teal, Icons.sms_rounded, false),
  IllustrationSlot.orderPlaced => (CastMember.padma, CastBackdrop.teal, Icons.check_rounded, true),
  IllustrationSlot.orderPacking => (CastMember.padma, CastBackdrop.ice, Icons.inventory_rounded, false),
  IllustrationSlot.orderOnTheWay => (CastMember.ravi, CastBackdrop.teal, Icons.delivery_dining_rounded, false),
  IllustrationSlot.orderDelivered => (CastMember.ammamma, CastBackdrop.teal, Icons.where_to_vote_rounded, false),
  IllustrationSlot.orderCancelled => (CastMember.ravi, CastBackdrop.coral, Icons.close_rounded, false),
  IllustrationSlot.complaintReceived => (CastMember.anil, CastBackdrop.teal, Icons.assignment_turned_in_rounded, false),
  IllustrationSlot.helpSupport => (CastMember.anil, CastBackdrop.sand, null, false),
  IllustrationSlot.errorGeneric => (CastMember.chanduva, CastBackdrop.coral, Icons.priority_high_rounded, false),
  IllustrationSlot.offline => (CastMember.ravi, CastBackdrop.ice, Icons.wifi_off_rounded, false),
  IllustrationSlot.notFound => (CastMember.chanduva, CastBackdrop.sand, Icons.explore_off_rounded, false),
  IllustrationSlot.homeHero => (CastMember.padma, CastBackdrop.teal, null, true),
  IllustrationSlot.featureHonestWeight => (CastMember.chanduva, CastBackdrop.sand, Icons.scale_rounded, false),
  IllustrationSlot.featureCleanedYourWay => (CastMember.padma, CastBackdrop.coral, Icons.restaurant_rounded, false),
  IllustrationSlot.featureColdChain => (CastMember.ravi, CastBackdrop.ice, Icons.ac_unit_rounded, false),
  IllustrationSlot.featureSameDay => (CastMember.ravi, CastBackdrop.teal, Icons.schedule_rounded, false),
};
