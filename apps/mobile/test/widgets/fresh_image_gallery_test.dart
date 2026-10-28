// FreshImageGallery (packages/flutter/fresh_theme/lib/src/gallery.dart): the product page's swipeable pictures.
// CI's mobile job runs only apps/mobile, so the theme package's parts are tested here. Pictures are coloured boxes
// (no network).

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_theme/fresh_theme.dart';

const _three = [
  FreshGalleryImage(url: 'https://example.test/1.webp', alt: 'Seer fish, whole'),
  FreshGalleryImage(url: 'https://example.test/2.webp', alt: 'Seer fish, steaks'),
  FreshGalleryImage(url: 'https://example.test/3.webp'),
];

Widget _picture(BuildContext context, FreshGalleryImage image, int index) =>
    ColoredBox(key: ValueKey('picture-$index'), color: Colors.teal);

Future<void> _pump(
  WidgetTester tester,
  List<FreshGalleryImage> images, {
  bool reduceMotion = false,
  ValueChanged<int>? onPageChanged,
}) async {
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = const Size(390 * 3, 900 * 3);
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.light(),
      builder: (context, app) => MediaQuery(
        data: MediaQuery.of(context).copyWith(disableAnimations: reduceMotion),
        child: app!,
      ),
      home: Scaffold(
        body: Align(
          alignment: Alignment.topLeft,
          child: SizedBox(
            width: 300,
            height: 200,
            child: FreshImageGallery(
              images: images,
              imageBuilder: _picture,
              emptyBuilder: (_) => const SizedBox.expand(key: Key('no-pictures')),
              onPageChanged: onPageChanged,
            ),
          ),
        ),
      ),
    ),
  );
}

Finder _dot(int index) => find.byKey(ValueKey('fresh-gallery-dot-$index'));

double _dotWidth(WidgetTester tester, int index) =>
    tester.getSize(find.byKey(ValueKey('fresh-gallery-dot-mark-$index'))).width;

void main() {
  group('FreshImageGallery', () {
    testWidgets('no pictures shows the empty builder, without pages or dots', (tester) async {
      await _pump(tester, const []);

      expect(find.byKey(const Key('no-pictures')), findsOneWidget);
      expect(find.byType(PageView), findsNothing);
      expect(_dot(0), findsNothing);
    });

    testWidgets('one picture fills the gallery, without pages, dots or a photo count', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(tester, const [FreshGalleryImage(url: 'https://example.test/1.webp')]);

      expect(tester.getSize(find.byKey(const ValueKey('picture-0'))), const Size(300, 200));
      expect(find.byType(PageView), findsNothing);
      expect(_dot(0), findsNothing);
      expect(find.bySemanticsLabel(RegExp(r'^Photo \d+ of \d+$')), findsNothing);
      handle.dispose();
    });

    testWidgets('more pictures: one full-size page each, a dot each, the first current and "Photo 1 of 3"', (
      tester,
    ) async {
      final handle = tester.ensureSemantics();
      await _pump(tester, _three);

      final pages = tester.widget<PageView>(find.byType(PageView)).childrenDelegate as SliverChildBuilderDelegate;
      expect(pages.childCount, 3);
      expect(tester.getSize(find.byKey(const ValueKey('picture-0'))), const Size(300, 200));
      for (var i = 0; i < 3; i++) {
        expect(_dot(i), findsOneWidget);
      }
      expect(_dot(3), findsNothing);
      expect(_dotWidth(tester, 0), 24);
      expect(_dotWidth(tester, 1), 7);
      expect(_dotWidth(tester, 2), 7);
      expect(find.bySemanticsLabel('Photo 1 of 3'), findsOneWidget);
      handle.dispose();
    });

    testWidgets('tapping the third dot slides to the third picture and makes its dot current', (tester) async {
      final handle = tester.ensureSemantics();
      final pages = <int>[];
      await _pump(tester, _three, onPageChanged: pages.add);

      await tester.tap(_dot(2));
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('picture-2')), findsOneWidget);
      expect(find.byKey(const ValueKey('picture-0')), findsNothing);
      expect(_dotWidth(tester, 2), 24);
      expect(_dotWidth(tester, 0), 7);
      expect(find.bySemanticsLabel('Photo 3 of 3'), findsOneWidget);
      expect(pages.last, 2);
      handle.dispose();
    });

    testWidgets('with reduced motion a dot shows its picture at once', (tester) async {
      await _pump(tester, _three, reduceMotion: true);

      await tester.tap(_dot(1));
      await tester.pump();

      expect(find.byKey(const ValueKey('picture-1')), findsOneWidget);
      expect(find.byKey(const ValueKey('picture-0')), findsNothing);
      expect(_dotWidth(tester, 1), 24);
      expect(tester.hasRunningAnimations, isFalse);
    });

    testWidgets('swiping moves to the next picture and moves the current dot', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(tester, _three);

      await tester.fling(find.byType(PageView), const Offset(-200, 0), 1000);
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('picture-1')), findsOneWidget);
      expect(_dotWidth(tester, 1), 24);
      expect(_dotWidth(tester, 0), 7);
      expect(find.bySemanticsLabel('Photo 2 of 3'), findsOneWidget);

      await tester.fling(find.byType(PageView), const Offset(200, 0), 1000);
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('picture-0')), findsOneWidget);
      expect(_dotWidth(tester, 0), 24);
      expect(find.bySemanticsLabel('Photo 1 of 3'), findsOneWidget);
      handle.dispose();
    });

    testWidgets('each dot is a button named for its picture that screen readers can press', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(tester, _three, reduceMotion: true);

      expect(
        tester.getSemantics(_dot(1)),
        isSemantics(label: 'Show photo 2', isButton: true, hasTapAction: true),
      );
      tester.semantics.tap(find.semantics.byLabel('Show photo 2'));
      await tester.pump();

      expect(find.byKey(const ValueKey('picture-1')), findsOneWidget);
      expect(find.bySemanticsLabel('Photo 2 of 3'), findsOneWidget);
      handle.dispose();
    });
  });
}
