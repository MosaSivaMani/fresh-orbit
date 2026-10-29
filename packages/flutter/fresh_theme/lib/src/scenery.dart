// Coastal scenery for the rich screens (D156, D176, D181): drawn gulls, clouds, hills, the drawn trawler, the
// sea bands and palms, traced from the approved previews in their 390-wide design space (callers scale the
// canvas); the owner's real trawler and gull pictures (D287, `FreshTrawler`, `FreshGull`); and the home harbour
// (the owner's sunrise harbour picture faded into the page, the drawn sky, sun, hills, sea and palm as its
// fallback, and the real boat with its gulls as a separate part, D320). Every colour and size is explicit.
// Scenery is decoration: wrap it in ExcludeSemantics (the widgets here do). Nothing moves.

import 'package:flutter/widgets.dart';

import 'highlights.dart';

/// Drawing helpers shared by the scenery painters. Coordinates are in the caller's canvas.
abstract final class FreshScenery {
  /// The sea colours under the onboarding sheet and in the home harbour.
  static const Color seaDeep = Color(0xFF9ED3CC);
  static const Color seaPale = Color(0xFFBFE3E0);

  /// The soft teal of palms that peek in at the screen edges.
  static const Color palmSoft = Color(0xFFBFE3E0);

  /// A gull: two little arcs starting at [at], [span] wide, rising [span] × [rise].
  static void bird(
    Canvas canvas,
    Offset at, {
    required double span,
    required Color color,
    required double strokeWidth,
    double rise = 0.3,
  }) {
    final half = span / 2;
    final path = Path()
      ..moveTo(at.dx, at.dy)
      ..relativeQuadraticBezierTo(half / 2, -span * rise, half, 0)
      ..relativeQuadraticBezierTo(half / 2, -span * rise, half, 0);
    canvas.drawPath(
      path,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = strokeWidth
        ..strokeCap = StrokeCap.round,
    );
  }

  /// A flat-bottomed cloud, 52 × 32 times [scale], its top-left at [at].
  static void cloud(Canvas canvas, Offset at, double scale, Color color) {
    canvas
      ..save()
      ..translate(at.dx, at.dy)
      ..scale(scale);
    final path = Path()
      ..moveTo(0, 16)
      ..arcToPoint(const Offset(26, 10), radius: const Radius.circular(14))
      ..arcToPoint(const Offset(48, 14), radius: const Radius.circular(12))
      ..arcToPoint(const Offset(52, 32), radius: const Radius.circular(10))
      ..lineTo(0, 32)
      ..arcToPoint(const Offset(0, 16), radius: const Radius.circular(10))
      ..close();
    canvas
      ..drawPath(path, Paint()..color = color)
      ..restore();
  }

  /// Two rolling hills with a ridge line, in the right part of a 390 × 393 design box (x 250–390, y 343–393).
  static void hills(Canvas canvas, Color fill) {
    canvas
      ..drawPath(
        Path()
          ..moveTo(250, 393)
          ..quadraticBezierTo(290, 353, 330, 363)
          ..quadraticBezierTo(370, 373, 390, 343)
          ..lineTo(390, 393)
          ..close(),
        Paint()..color = fill,
      )
      ..drawPath(
        Path()
          ..moveTo(300, 370)
          ..quadraticBezierTo(320, 362, 340, 370)
          ..quadraticBezierTo(360, 378, 380, 366),
        Paint()
          ..color = fill.withValues(alpha: fill.a * 0.9)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2
          ..strokeCap = StrokeCap.round,
      );
  }

  /// The fishing trawler (curved hull, cabin with a window row, wheelhouse, mast with a flag and rigging), in a
  /// design box where the hull sits at x 2–120, y 250–364. Only the sign-in coast still draws it; the onboarding
  /// and the home show the owner's real trawler instead ([FreshTrawler], D287).
  static void trawler(Canvas canvas, Color fill, {Color window = const Color(0x47FFFDF8)}) {
    final paint = Paint()..color = fill;
    canvas
      ..drawPath(
        Path()
          ..moveTo(2, 330)
          ..lineTo(120, 330)
          ..quadraticBezierTo(114, 352, 94, 364)
          ..lineTo(20, 364)
          ..quadraticBezierTo(8, 354, 2, 330)
          ..close(),
        paint,
      )
      ..drawRRect(RRect.fromLTRBR(26, 306, 80, 331, const Radius.circular(2)), paint)
      ..drawRRect(RRect.fromLTRBR(34, 292, 68, 307, const Radius.circular(2)), paint);
    final glass = Paint()..color = window;
    for (final r in const [
      Rect.fromLTWH(32, 312, 9, 8),
      Rect.fromLTWH(45, 312, 9, 8),
      Rect.fromLTWH(58, 312, 9, 8),
      Rect.fromLTWH(40, 296, 7, 6),
      Rect.fromLTWH(51, 296, 7, 6),
    ]) {
      canvas.drawRect(r, glass);
    }
    canvas
      ..drawRect(const Rect.fromLTWH(84, 250, 3.5, 82), paint)
      ..drawRect(const Rect.fromLTWH(52, 268, 3, 26), paint)
      ..drawPath(
        Path()
          ..moveTo(87.5, 252)
          ..lineTo(103.5, 257)
          ..lineTo(87.5, 263)
          ..close(),
        paint,
      );
    final rig = Paint()
      ..color = fill
      ..strokeWidth = 1.4;
    canvas
      ..drawLine(const Offset(86, 256), const Offset(118, 330), rig)
      ..drawLine(const Offset(86, 262), const Offset(8, 330), rig)
      ..drawLine(const Offset(54, 270), const Offset(86, 270), rig)
      ..drawLine(const Offset(98, 300), const Offset(114, 300), rig);
  }

  /// Deep and pale sea bands with a wave line, x 0–180, y 350–424 of the design box (they run under a sheet).
  static void seaBands(
    Canvas canvas, {
    Color deep = seaDeep,
    Color pale = seaPale,
    Color line = const Color(0xE6FFFDF8),
  }) {
    canvas
      ..drawPath(
        Path()
          ..moveTo(0, 360)
          ..quadraticBezierTo(30, 350, 60, 358)
          ..quadraticBezierTo(90, 366, 120, 358)
          ..quadraticBezierTo(150, 350, 170, 364)
          ..lineTo(170, 424)
          ..lineTo(0, 424)
          ..close(),
        Paint()..color = deep,
      )
      ..drawPath(
        Path()
          ..moveTo(0, 376)
          ..quadraticBezierTo(30, 368, 60, 376)
          ..quadraticBezierTo(90, 384, 120, 376)
          ..quadraticBezierTo(150, 368, 180, 380),
        Paint()
          ..color = line
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2.4
          ..strokeCap = StrokeCap.round,
      )
      ..drawPath(
        Path()
          ..moveTo(0, 388)
          ..quadraticBezierTo(40, 382, 80, 388)
          ..quadraticBezierTo(120, 394, 170, 392)
          ..lineTo(170, 412)
          ..lineTo(0, 412)
          ..close(),
        Paint()..color = pale,
      );
  }

  /// A palm: a curved trunk 120 tall growing up to [at] (the crown) and five leaves, scaled by [scale];
  /// [mirrored] flips it left to right. Leaves reach about 54 left and 60 right of the crown, 30 above it.
  static void palm(Canvas canvas, Offset at, {required double scale, required Color color, bool mirrored = false}) {
    canvas
      ..save()
      ..translate(at.dx, at.dy)
      ..scale(mirrored ? -scale : scale, scale);
    final paint = Paint()..color = color;
    canvas
      ..drawPath(
        Path()
          ..moveTo(0, 120)
          ..cubicTo(4, 80, 6, 44, 2, 16)
          ..lineTo(8, 16)
          ..cubicTo(12, 46, 10, 82, 6, 120)
          ..close(),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(4, 16)
          ..cubicTo(-16, 2, -40, 6, -54, 20)
          ..cubicTo(-36, 12, -16, 12, 4, 20)
          ..close(),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(6, 16)
          ..cubicTo(0, -4, -18, -14, -38, -12)
          ..cubicTo(-20, -6, -6, 2, 6, 16)
          ..close(),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(6, 14)
          ..cubicTo(20, -4, 42, -6, 60, 4)
          ..cubicTo(40, 4, 22, 8, 6, 16)
          ..close(),
        paint,
      )
      ..drawPath(
        Path()
          ..moveTo(6, 16)
          ..cubicTo(28, 12, 50, 22, 58, 38)
          ..cubicTo(42, 26, 24, 22, 6, 18)
          ..close(),
        paint,
      )
      ..restore();
  }
}

/// One palm for [PalmsPainter]: [at] is the crown, measured from the right edge when [fromRight] is set.
@immutable
class ScenePalm {
  const ScenePalm({
    required this.at,
    required this.scale,
    this.color = FreshScenery.palmSoft,
    this.mirrored = false,
    this.fromRight = false,
  });

  final Offset at;
  final double scale;
  final Color color;
  final bool mirrored;
  final bool fromRight;
}

/// Palms peeking in at the edges of the painter's box (the home page's soft palms). It paints outside its box.
class PalmsPainter extends CustomPainter {
  const PalmsPainter(this.palms);

  final List<ScenePalm> palms;

  @override
  void paint(Canvas canvas, Size size) {
    for (final p in palms) {
      final at = p.fromRight ? Offset(size.width - p.at.dx, p.at.dy) : p.at;
      FreshScenery.palm(canvas, at, scale: p.scale, color: p.color, mirrored: p.mirrored);
    }
  }

  @override
  bool shouldRepaint(PalmsPainter old) => !identical(old.palms, palms);
}

/// The owner's real scenery pictures (D287), bundled in this package under `assets/scenery/`: the fishing trawler
/// on its own splashing waves (600 × 571, the sea's cut edges faded out so it melts into any drawn sea) and three
/// gulls in flight (cut-outs, 160 on the long side). All transparent PNGs, loaded with `package: 'fresh_theme'`.
abstract final class FreshSceneryPictures {
  static const String package = 'fresh_theme';
  static const String trawler = 'assets/scenery/trawler.png';

  /// The owner's sunrise harbour (D284, D320): the website hero picture cropped to the sun and the open sea
  /// (x 300–1500, y 142–742 of the 2119 × 742 original) and saved 800 × 400, about 80 KB. The home's corner.
  static const String harbour = 'assets/scenery/harbour.jpg';

  /// Width ÷ height of the harbour picture.
  static const double harbourAspect = 800 / 400;

  /// Width ÷ height of the trawler picture.
  static const double trawlerAspect = 600 / 571;

  /// The three gulls; [gullAspects] holds each one's width ÷ height.
  static const List<String> gulls = [
    'assets/scenery/gull_1.png',
    'assets/scenery/gull_2.png',
    'assets/scenery/gull_3.png',
  ];
  static const List<double> gullAspects = [160 / 96, 133 / 160, 160 / 139];
}

/// The real fishing trawler (D287), [width] wide; its height follows the picture. Decoration: the picture is
/// excluded from semantics. [mirrored] turns it to sail the other way; [opacity] fades a far-off boat.
class FreshTrawler extends StatelessWidget {
  const FreshTrawler({super.key, required this.width, this.mirrored = false, this.opacity = 1});

  final double width;
  final bool mirrored;
  final double opacity;

  /// The picture's height at [width].
  static double heightFor(double width) => width / FreshSceneryPictures.trawlerAspect;

  @override
  Widget build(BuildContext context) => _ScenePicture(
    asset: FreshSceneryPictures.trawler,
    width: width,
    height: heightFor(width),
    mirrored: mirrored,
    opacity: opacity,
  );
}

/// One of the three real gulls (D287), [width] wide ([variant] 0, 1 or 2, wrapping round). Decoration only.
class FreshGull extends StatelessWidget {
  const FreshGull({super.key, required this.width, this.variant = 0, this.mirrored = false});

  final double width;
  final int variant;
  final bool mirrored;

  /// The gull's height at [width].
  static double heightFor(double width, {int variant = 0}) =>
      width / FreshSceneryPictures.gullAspects[variant % FreshSceneryPictures.gulls.length];

  @override
  Widget build(BuildContext context) => _ScenePicture(
    asset: FreshSceneryPictures.gulls[variant % FreshSceneryPictures.gulls.length],
    width: width,
    height: heightFor(width, variant: variant),
    mirrored: mirrored,
    opacity: 1,
  );
}

/// A bundled scenery picture at a fixed size; while it loads, or if it cannot, the box stays empty.
class _ScenePicture extends StatelessWidget {
  const _ScenePicture({
    required this.asset,
    required this.width,
    required this.height,
    required this.mirrored,
    required this.opacity,
  });

  final String asset;
  final double width;
  final double height;
  final bool mirrored;
  final double opacity;

  @override
  Widget build(BuildContext context) {
    Widget picture = Image.asset(
      asset,
      package: FreshSceneryPictures.package,
      width: width,
      height: height,
      fit: BoxFit.contain,
      opacity: opacity < 1 ? AlwaysStoppedAnimation(opacity) : null,
      filterQuality: FilterQuality.medium,
      excludeFromSemantics: true,
      gaplessPlayback: true,
      errorBuilder: (_, _, _) => SizedBox(width: width, height: height),
    );
    if (mirrored) picture = Transform.flip(flipX: true, child: picture);
    return picture;
  }
}

/// The home harbour's drawn backdrop (D176 preview, x 168–390 and y 56–206 of the 390-wide screen): a warm sky,
/// the sun, low hills, the sea with a wave line and a teal palm cut by the right edge. Since D320 it is only the
/// fallback [HarbourScene] shows when the owner's harbour picture cannot load. Designed in a 222 × 150 box
/// ([designSize]) and scaled to the painter's width.
class HarbourScenePainter extends CustomPainter {
  const HarbourScenePainter({
    this.skyTop = const Color(0xFFF7F1E6),
    this.skyMid = const Color(0xFFFCE1C2),
    this.skyBottom = const Color(0xFFF9CFA4),
    this.sun = const Color(0xE6F8B65F),
    this.hills = const Color(0x8CE9B79A),
    this.sea = FreshScenery.seaDeep,
    this.wave = FreshHighlightColors.paper,
    this.palm = const Color(0xFF0B7D70),
  });

  static const Size designSize = Size(222, 150);

  final Color skyTop;
  final Color skyMid;
  final Color skyBottom;
  final Color sun;
  final Color hills;
  final Color sea;
  final Color wave;
  final Color palm;

  @override
  void paint(Canvas canvas, Size size) {
    final k = size.width / designSize.width;
    canvas
      ..save()
      ..scale(k);
    final sky = Path()
      ..moveTo(0, 8)
      ..quadraticBezierTo(110, -16, 222, 0)
      ..lineTo(222, 150)
      ..lineTo(0, 150)
      ..close();
    canvas
      ..drawPath(
        sky,
        Paint()
          ..shader = LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [skyTop, skyMid, skyBottom],
            stops: const [0, 0.55, 1],
          ).createShader(const Rect.fromLTWH(0, -4, 222, 154)),
      )
      ..drawCircle(const Offset(162, 94), 20, Paint()..color = sun)
      ..drawPath(
        Path()
          ..moveTo(0, 94)
          ..relativeQuadraticBezierTo(40, -14, 70, -6)
          ..relativeQuadraticBezierTo(30, -16, 60, -4)
          ..relativeQuadraticBezierTo(40, -10, 92, 0)
          ..lineTo(222, 114)
          ..lineTo(0, 114)
          ..close(),
        Paint()..color = hills,
      )
      ..drawPath(
        Path()
          ..moveTo(0, 120)
          ..quadraticBezierTo(50, 110, 110, 118)
          ..quadraticBezierTo(170, 126, 222, 112)
          ..lineTo(222, 152)
          ..lineTo(0, 152)
          ..close(),
        Paint()..color = sea,
      )
      ..drawPath(
        Path()
          ..moveTo(12, 130)
          ..quadraticBezierTo(42, 125, 72, 130)
          ..quadraticBezierTo(102, 135, 132, 130)
          ..quadraticBezierTo(162, 125, 192, 132),
        Paint()
          ..color = wave
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2
          ..strokeCap = StrokeCap.round,
      );
    FreshScenery.palm(canvas, const Offset(204, 18), scale: 0.95, color: palm);
    canvas.restore();
  }

  @override
  bool shouldRepaint(HarbourScenePainter old) =>
      old.skyTop != skyTop ||
      old.skyMid != skyMid ||
      old.skyBottom != skyBottom ||
      old.sun != sun ||
      old.hills != hills ||
      old.sea != sea ||
      old.wave != wave ||
      old.palm != palm;
}

/// The home harbour as a widget (D320), [width] wide and half as tall, excluded from semantics: the owner's
/// sunrise harbour picture ([FreshSceneryPictures.harbour]: the sun and the open sea, the boats at the far left)
/// whose left, top and bottom edges fade into the page through two gradient masks, so it has no hard edge. If
/// the picture cannot load, the drawn backdrop ([fallback]) shows in the same faded box instead. It holds no boat
/// and no gull: the screen places [HarbourBoat] in room of its own, so nothing covers text. Nothing moves.
class HarbourScene extends StatelessWidget {
  const HarbourScene({
    super.key,
    this.width = defaultWidth,
    this.picture,
    this.fallback = const HarbourScenePainter(),
  });

  static const double defaultWidth = 240;

  final double width;

  /// The picture to show; null shows the bundled harbour. Tests pass a broken one to see the fallback.
  final ImageProvider? picture;

  /// The drawn backdrop shown when the picture fails to load.
  final HarbourScenePainter fallback;

  /// The scene's height at [width] (the picture's 2 : 1 shape).
  static double heightFor(double width) => width / FreshSceneryPictures.harbourAspect;

  /// Where the fades end: the left 42 % fades in from nothing, the top 12 % too, and the lower 45 % fades out.
  static const double leftFade = 0.42;
  static const double topFade = 0.12;
  static const double bottomFadeStart = 0.55;

  @override
  Widget build(BuildContext context) {
    final height = heightFor(width);
    final drawnWidth = height * HarbourScenePainter.designSize.width / HarbourScenePainter.designSize.height;
    final drawn = Align(
      alignment: Alignment.centerRight,
      child: CustomPaint(
        key: const Key('harbourDrawn'),
        size: Size(drawnWidth, height),
        painter: fallback,
      ),
    );
    return ExcludeSemantics(
      child: SizedBox(
        width: width,
        height: height,
        child: ShaderMask(
          blendMode: BlendMode.dstIn,
          shaderCallback: (rect) => const LinearGradient(
            colors: [Color(0x00000000), Color(0xFF000000)],
            stops: [0, leftFade],
          ).createShader(rect),
          child: ShaderMask(
            blendMode: BlendMode.dstIn,
            shaderCallback: (rect) => const LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [Color(0x00000000), Color(0xFF000000), Color(0xFF000000), Color(0x00000000)],
              stops: [0, topFade, bottomFadeStart, 1],
            ).createShader(rect),
            child: ClipRect(
              child: Image(
                key: const Key('harbourPicture'),
                image: picture ?? const AssetImage(FreshSceneryPictures.harbour, package: FreshSceneryPictures.package),
                width: width,
                height: height,
                fit: BoxFit.cover,
                alignment: Alignment.centerRight,
                filterQuality: FilterQuality.medium,
                excludeFromSemantics: true,
                gaplessPlayback: true,
                errorBuilder: (_, _, _) => drawn,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The home harbour's boat (D287, D320): the owner's real trawler, [shipWidth] wide, with three small real gulls
/// ahead of its bow, excluded from semantics. It is laid out in the page's flow (the home puts it at the end of
/// the greeting line, [headingClearance] above the line's bottom), so it reserves its own room and never covers
/// the heading, its sparks or the handwritten line at any width or text size.
class HarbourBoat extends StatelessWidget {
  const HarbourBoat({super.key, this.shipWidth = 44});

  final double shipWidth;

  /// The room left of the ship that the gulls fly in.
  static const double gullRoom = 26;

  /// How far the heading's sparks reach above the heading's box (Sparks at top −16 inside its 6 dp top padding):
  /// keep at least this much between the boat's bottom and the line below it.
  static const double headingClearance = 10;

  /// The gulls in the [gullRoom]: each one's top-left, width and variant.
  static const List<(Offset, double, int)> _gulls = [
    (Offset(4, 0), 14.0, 0),
    (Offset(16, 11), 10.0, 2),
    (Offset(1, 18), 9.0, 1),
  ];

  /// The boat's size for [shipWidth].
  static Size sizeFor(double shipWidth) => Size(shipWidth + gullRoom, FreshTrawler.heightFor(shipWidth));

  @override
  Widget build(BuildContext context) {
    return ExcludeSemantics(
      child: SizedBox.fromSize(
        size: sizeFor(shipWidth),
        child: Stack(
          children: [
            Positioned(
              right: 0,
              bottom: 0,
              child: FreshTrawler(key: const Key('harbourTrawler'), width: shipWidth),
            ),
            for (final (at, span, variant) in _gulls)
              Positioned(
                left: at.dx,
                top: at.dy,
                child: FreshGull(width: span, variant: variant),
              ),
          ],
        ),
      ),
    );
  }
}

