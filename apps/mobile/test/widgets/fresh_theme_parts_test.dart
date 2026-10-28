// The shared parts of the theme package (packages/flutter/fresh_theme: layout, highlights, scenery). CI's
// mobile job runs only apps/mobile, so they are tested here.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_theme/fresh_theme.dart';

Future<void> _pump(WidgetTester tester, Widget child, {double width = 390}) async {
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = Size(width * 3, 900 * 3);
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.light(),
      home: Scaffold(body: Align(alignment: Alignment.topLeft, child: child)),
    ),
  );
}

void main() {
  group('FreshLayout', () {
    test('gutter, gaps and radii follow the home preview', () {
      expect(FreshLayout.gutter, 16);
      expect(FreshLayout.cardGap, 12);
      expect(FreshLayout.sectionGap, 24);
      expect(FreshLayout.heroRadius, 24);
      expect(FreshLayout.pillRadius, 99);
      expect(FreshLayout.contentWidth(390), 358);
      expect(FreshLayout.columnWidth(390, 2), 173);
      expect(FreshLayout.columnWidth(390, 6, gap: 0), closeTo(59.67, 0.01));
      expect(FreshLayout.columnWidth(20, 3), 0);
    });

    testWidgets('FreshColumns gives every child one equal column inside the gutters', (tester) async {
      await _pump(
        tester,
        const Padding(
          padding: FreshLayout.page,
          child: FreshColumns(
            children: [
              SizedBox(key: Key('a'), height: 40),
              SizedBox(key: Key('b'), height: 80),
              SizedBox(key: Key('c'), height: 20),
            ],
          ),
        ),
        width: 360,
      );
      final rects = [for (final k in ['a', 'b', 'c']) tester.getRect(find.byKey(Key(k)))];
      final width = FreshLayout.columnWidth(360, 3);
      expect(rects.first.left, 16);
      expect(rects.last.right, 360 - 16);
      for (final r in rects) {
        expect(r.width, moreOrLessEquals(width, epsilon: 0.01));
      }
      expect(rects[1].left - rects[0].right, moreOrLessEquals(12, epsilon: 0.01));
    });

    testWidgets('FreshColumns with stretch makes the columns as tall as the tallest', (tester) async {
      await _pump(
        tester,
        const SizedBox(
          width: 300,
          child: FreshColumns(
            stretch: true,
            children: [
              ColoredBox(key: Key('short'), color: Colors.red, child: SizedBox(height: 30)),
              ColoredBox(key: Key('tall'), color: Colors.blue, child: SizedBox(height: 90)),
            ],
          ),
        ),
      );
      expect(tester.getSize(find.byKey(const Key('short'))).height, 90);
      expect(tester.getSize(find.byKey(const Key('tall'))).height, 90);
    });

    testWidgets('FreshColumns with stretch measures again when a column grows later (no overflow)', (tester) async {
      final grow = ValueNotifier<double>(30);
      addTearDown(grow.dispose);
      await _pump(
        tester,
        SizedBox(
          width: 300,
          child: FreshColumns(
            stretch: true,
            children: [
              ValueListenableBuilder<double>(
                valueListenable: grow,
                builder: (_, height, _) => Column(
                  key: const Key('growing'),
                  children: [SizedBox(height: height)],
                ),
              ),
              const ColoredBox(key: Key('other'), color: Colors.blue, child: SizedBox(height: 90)),
            ],
          ),
        ),
      );
      expect(tester.getSize(find.byKey(const Key('growing'))).height, 90);

      grow.value = 140;
      await tester.pump();

      expect(tester.takeException(), isNull);
      expect(tester.getSize(find.byKey(const Key('growing'))).height, 140);
      expect(tester.getSize(find.byKey(const Key('other'))).height, 140);
    });

    testWidgets('FreshFitWidth keeps the full width and scales a too-tall child to the height', (tester) async {
      await _pump(
        tester,
        const SizedBox(
          width: 300,
          height: 200,
          child: Align(
            alignment: Alignment.topCenter,
            child: FreshFitWidth(
              key: Key('fit'),
              child: SizedBox(key: Key('page'), width: double.infinity, height: 400),
            ),
          ),
        ),
      );
      final fit = tester.getRect(find.byKey(const Key('fit')));
      final page = tester.getRect(find.byKey(const Key('page')));
      expect(fit.width, 300);
      expect(fit.height, moreOrLessEquals(200, epsilon: 0.01));
      // Laid out twice as wide, drawn at half size: edge to edge, no side gaps.
      expect(page.left, fit.left);
      expect(page.width, moreOrLessEquals(300, epsilon: 0.01));
      expect(tester.getSize(find.byKey(const Key('page'))).width, moreOrLessEquals(600, epsilon: 0.01));
    });

    testWidgets('FreshFitWidth leaves a child that fits at scale 1', (tester) async {
      await _pump(
        tester,
        const SizedBox(
          width: 300,
          height: 200,
          child: Align(
            alignment: Alignment.topCenter,
            child: FreshFitWidth(child: SizedBox(key: Key('page'), width: double.infinity, height: 120)),
          ),
        ),
      );
      expect(tester.getRect(find.byKey(const Key('page'))), const Rect.fromLTWH(0, 0, 300, 120));
    });
  });

  group('highlights', () {
    testWidgets('BrushHighlight paints behind its child and keeps the child readable', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(tester, const BrushHighlight(degrees: -2, child: Text('today?')));

      expect(find.bySemanticsLabel('today?'), findsOneWidget);
      final paint = tester.widget<CustomPaint>(
        find.descendant(of: find.byType(BrushHighlight), matching: find.byType(CustomPaint)).first,
      );
      expect(paint.painter, isA<BrushPainter>());
      expect((paint.painter! as BrushPainter).color, FreshHighlightColors.brush);
      expect(find.byType(Transform), findsWidgets);
      handle.dispose();
    });

    testWidgets('Sparks are 34 × 30 times the scale and not announced', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(tester, const Sparks(color: FreshHighlightColors.coral, scale: 0.5, degrees: 30));

      final paint = find.descendant(of: find.byType(Sparks), matching: find.byType(CustomPaint));
      expect(tester.getSize(paint), const Size(17, 15));
      expect(find.descendant(of: find.byType(Sparks), matching: find.byType(ExcludeSemantics)), findsOneWidget);
      expect(tester.takeException(), isNull);
      handle.dispose();
    });

    testWidgets('ScriptLine uses the family the app registers, Dancing Script 700, and a swoosh', (tester) async {
      await _pump(
        tester,
        const ScriptLine(
          text: 'From our coast to your kitchen',
          fontFamily: AppFonts.script,
          color: FreshHighlightColors.coralDeep,
          fontSize: 19,
          swooshSize: Size(140, 9),
        ),
      );

      final text = tester.widget<Text>(find.text('From our coast to your kitchen'));
      expect(text.style!.fontFamily, AppFonts.script);
      expect(text.style!.fontWeight, FontWeight.w700);
      expect(text.style!.fontVariations, const [FontVariation('wght', 700)]);
      expect(text.style!.fontSize, 19);
      expect(tester.getSize(find.byType(SwooshUnderline)), const Size(140, 9));
    });

    testWidgets('ScriptLine leaves the swoosh out when its width is zero', (tester) async {
      await _pump(
        tester,
        const ScriptLine(text: 'Hi', fontFamily: 'X', color: Colors.black, fontSize: 12, swooshSize: Size.zero),
      );
      expect(find.byType(SwooshUnderline), findsNothing);
    });

    testWidgets('Snowflake and SwooshUnderline are decoration only', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(
        tester,
        const Column(
          children: [
            Snowflake(color: Colors.teal, scale: 2),
            SwooshUnderline(color: Colors.orange),
          ],
        ),
      );
      expect(tester.getSize(find.byType(Snowflake)), const Size(60, 52));
      expect(tester.getSize(find.byType(SwooshUnderline)), const Size(92, 12));
      for (final type in [Snowflake, SwooshUnderline]) {
        expect(find.descendant(of: find.byType(type), matching: find.byType(ExcludeSemantics)), findsOneWidget);
      }
      handle.dispose();
    });
  });

  group('scenery', () {
    testWidgets('HarbourScene keeps the 222 × 150 shape and is excluded from semantics', (tester) async {
      await _pump(tester, const HarbourScene(width: 111));

      expect(tester.getSize(find.byType(HarbourScene)), const Size(111, 75));
      expect(find.descendant(of: find.byType(HarbourScene), matching: find.byType(ExcludeSemantics)), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('HarbourScene shows the real trawler, a far boat and four real gulls inside its box', (tester) async {
      await _pump(tester, const HarbourScene());

      final box = tester.getRect(find.byType(HarbourScene));
      final ship = find.byKey(const Key('harbourTrawler'));
      expect(ship, findsOneWidget);
      expect(find.byKey(const Key('harbourFarTrawler')), findsOneWidget);
      expect(find.byType(FreshGull), findsNWidgets(4));
      final picture = tester.widget<Image>(find.descendant(of: ship, matching: find.byType(Image))).image;
      expect(picture, isA<AssetImage>());
      expect((picture as AssetImage).assetName, FreshSceneryPictures.trawler);
      expect(picture.package, FreshSceneryPictures.package);
      // The big boat sits on the sea in the lower half; nothing reaches outside the scene.
      expect(tester.getRect(ship).bottom, greaterThan(box.top + box.height * 0.75));
      for (final part in [find.byType(FreshTrawler), find.byType(FreshGull)]) {
        for (var i = 0; i < part.evaluate().length; i++) {
          final r = tester.getRect(part.at(i));
          expect(box.contains(r.topLeft) && box.contains(r.bottomRight - const Offset(0.01, 0.01)), isTrue);
        }
      }
      expect(tester.takeException(), isNull);
    });

    testWidgets('FreshTrawler and FreshGull keep the shape of their pictures', (tester) async {
      await _pump(tester, const Column(children: [FreshTrawler(width: 120), FreshGull(width: 30, variant: 4)]));

      expect(tester.getSize(find.byType(FreshTrawler)).width, 120);
      expect(
        tester.getSize(find.byType(FreshTrawler)).height,
        closeTo(120 / FreshSceneryPictures.trawlerAspect, 0.01),
      );
      // Variant 4 wraps round to the second gull.
      expect(tester.getSize(find.byType(FreshGull)).height, closeTo(30 / FreshSceneryPictures.gullAspects[1], 0.01));
      final gull = tester.widget<Image>(find.descendant(of: find.byType(FreshGull), matching: find.byType(Image)));
      expect((gull.image as AssetImage).assetName, FreshSceneryPictures.gulls[1]);
    });

    testWidgets('the scenery pictures are bundled with the theme package', (tester) async {
      for (final asset in [FreshSceneryPictures.trawler, ...FreshSceneryPictures.gulls]) {
        final data = await tester.runAsync(() => rootBundle.load('packages/${FreshSceneryPictures.package}/$asset'));
        expect(data!.lengthInBytes, greaterThan(1000), reason: asset);
      }
    });

    testWidgets('PalmsPainter draws palms from either edge without errors', (tester) async {
      await _pump(
        tester,
        const SizedBox(
          width: 300,
          height: 300,
          child: CustomPaint(
            painter: PalmsPainter([
              ScenePalm(at: Offset(-6, 100), scale: 0.9, mirrored: true),
              ScenePalm(at: Offset(-4, 200), scale: 0.6, fromRight: true, color: FreshScenery.seaDeep),
            ]),
          ),
        ),
      );
      expect(tester.takeException(), isNull);
    });

    test('the painters repaint only when their colours change', () {
      expect(const SparksPainter(Colors.red).shouldRepaint(const SparksPainter(Colors.red)), isFalse);
      expect(const SparksPainter(Colors.red).shouldRepaint(const SparksPainter(Colors.blue)), isTrue);
      expect(const HarbourScenePainter().shouldRepaint(const HarbourScenePainter()), isFalse);
      expect(const HarbourScenePainter().shouldRepaint(const HarbourScenePainter(sun: Colors.red)), isTrue);
      expect(const BrushPainter(Colors.red).shouldRepaint(const BrushPainter(Colors.red)), isFalse);
    });
  });
}
