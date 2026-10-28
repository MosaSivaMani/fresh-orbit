// FreshGallery (fresh_theme, D266, D276): the one picture gallery of the app. Every picture swipeable, with
// tappable page dots; one picture alone; none shows the empty widget; screen readers move with increase /
// decrease or a dot's button. CI's mobile job runs only apps/mobile, so the package part is tested here.
// Pictures are coloured boxes (no network).

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_theme/fresh_theme.dart';

Future<void> _pump(
  WidgetTester tester,
  int count, {
  bool reduceMotion = false,
  ValueChanged<int>? onPageChanged,
}) async {
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = const Size(390 * 3, 800 * 3);
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
            child: FreshGallery(
              itemCount: count,
              empty: const SizedBox(key: Key('noPicture')),
              onPageChanged: onPageChanged,
              itemBuilder: (_, i) => Semantics(
                label: 'Seer fish $i',
                image: true,
                child: ColoredBox(key: ValueKey('picture-$i'), color: Colors.teal),
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

Finder _dot(int index) => find.byKey(ValueKey('freshGalleryDot-$index'));

double _dotWidth(WidgetTester tester, int index) =>
    tester.getSize(find.byKey(ValueKey('freshGalleryDotMark-$index'))).width;

int _current(WidgetTester tester, int count) {
  final wide = [for (var i = 0; i < count; i++) if (_dotWidth(tester, i) == 24) i];
  expect(wide, hasLength(1));
  return wide.single;
}

void main() {
  testWidgets('no picture shows the empty widget, without pages or dots', (tester) async {
    await _pump(tester, 0);

    expect(find.byKey(const Key('noPicture')), findsOneWidget);
    expect(find.byType(PageView), findsNothing);
    expect(find.byKey(const Key('freshGalleryDots')), findsNothing);
  });

  testWidgets('one picture fills the gallery alone, without dots, paging or a picture count', (tester) async {
    final handle = tester.ensureSemantics();
    await _pump(tester, 1);

    expect(find.byKey(const Key('freshGallerySingle')), findsOneWidget);
    expect(tester.getSize(find.byKey(const ValueKey('picture-0'))), const Size(300, 200));
    expect(find.byType(PageView), findsNothing);
    expect(find.byKey(const Key('freshGalleryDots')), findsNothing);
    expect(find.bySemanticsLabel(RegExp(r'^Picture \d+ of \d+$')), findsNothing);
    handle.dispose();
  });

  testWidgets('more pictures: one full-size page each, a dot each, the first current and "Picture 1 of 3"', (
    tester,
  ) async {
    final handle = tester.ensureSemantics();
    await _pump(tester, 3);

    expect(tester.getSize(find.byKey(const ValueKey('picture-0'))), const Size(300, 200));
    for (var i = 0; i < 3; i++) {
      expect(_dot(i), findsOneWidget);
    }
    expect(_dot(3), findsNothing);
    expect(_dotWidth(tester, 0), 24);
    expect(_dotWidth(tester, 1), 7);
    expect(_dotWidth(tester, 2), 7);
    expect(find.bySemanticsLabel(RegExp(r'Picture 1 of 3')), findsOneWidget);
    handle.dispose();
  });

  testWidgets('each picture keeps the alt text its builder gives', (tester) async {
    final handle = tester.ensureSemantics();
    await _pump(tester, 3);

    expect(find.bySemanticsLabel(RegExp('Seer fish 0')), findsWidgets);
    handle.dispose();
  });

  for (final count in [2, 3, 4]) {
    testWidgets('$count pictures swipe one by one and the dots follow', (tester) async {
      await _pump(tester, count);

      expect(find.byKey(const Key('freshGalleryDots')), findsOneWidget);
      expect(_current(tester, count), 0);

      for (var i = 1; i < count; i++) {
        await tester.drag(find.byKey(const Key('freshGalleryPages')), const Offset(-250, 0));
        await tester.pumpAndSettle();
        expect(_current(tester, count), i);
        expect(find.byKey(ValueKey('picture-$i')), findsOneWidget);
      }

      // The last picture stays the last one.
      await tester.drag(find.byKey(const Key('freshGalleryPages')), const Offset(-250, 0));
      await tester.pumpAndSettle();
      expect(_current(tester, count), count - 1);
    });
  }

  testWidgets('swiping back moves to the previous picture', (tester) async {
    await _pump(tester, 3);

    await tester.fling(find.byType(PageView), const Offset(-200, 0), 1000);
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('picture-1')), findsOneWidget);
    expect(_current(tester, 3), 1);

    await tester.fling(find.byType(PageView), const Offset(200, 0), 1000);
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('picture-0')), findsOneWidget);
    expect(_current(tester, 3), 0);
  });

  testWidgets('tapping the third dot slides to the third picture and makes its dot current', (tester) async {
    final handle = tester.ensureSemantics();
    final pages = <int>[];
    await _pump(tester, 3, onPageChanged: pages.add);

    await tester.tap(_dot(2));
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('picture-2')), findsOneWidget);
    expect(find.byKey(const ValueKey('picture-0')), findsNothing);
    expect(_current(tester, 3), 2);
    expect(find.bySemanticsLabel(RegExp(r'Picture 3 of 3')), findsOneWidget);
    expect(pages.last, 2);
    handle.dispose();
  });

  testWidgets('with reduced motion a dot shows its picture at once', (tester) async {
    await _pump(tester, 3, reduceMotion: true);

    await tester.tap(_dot(1));
    await tester.pump();

    expect(find.byKey(const ValueKey('picture-1')), findsOneWidget);
    expect(find.byKey(const ValueKey('picture-0')), findsNothing);
    expect(_current(tester, 3), 1);
    expect(tester.hasRunningAnimations, isFalse);
  });

  testWidgets('each dot is a button named for its picture that screen readers can press', (tester) async {
    final handle = tester.ensureSemantics();
    await _pump(tester, 3, reduceMotion: true);

    expect(
      tester.getSemantics(_dot(1)),
      isSemantics(label: 'Show picture 2', isButton: true, hasTapAction: true),
    );
    tester.semantics.tap(find.semantics.byLabel('Show picture 2'));
    await tester.pump();

    expect(find.byKey(const ValueKey('picture-1')), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'Picture 2 of 3')), findsOneWidget);
    handle.dispose();
  });

  testWidgets('screen readers can move with increase and decrease (no swipe needed)', (tester) async {
    await _pump(tester, 3);

    Semantics gallery() => tester.widget<Semantics>(find.byKey(const Key('freshGallery')));
    expect(gallery().properties.onDecrease, isNull); // first picture
    gallery().properties.onIncrease!();
    await tester.pumpAndSettle();
    expect(_current(tester, 3), 1);

    gallery().properties.onDecrease!();
    await tester.pumpAndSettle();
    expect(_current(tester, 3), 0);
  });
}
