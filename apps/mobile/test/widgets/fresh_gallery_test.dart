// FreshGallery (fresh_theme, D262): every picture, swipeable, with page dots; one picture alone; none shows the
// empty widget. CI's mobile job runs only apps/mobile, so the package part is tested here.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_theme/fresh_theme.dart';

Future<void> _pump(WidgetTester tester, int count) async {
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = const Size(390 * 3, 800 * 3);
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.light(),
      home: Scaffold(
        body: SizedBox(
          width: 358,
          height: 239,
          child: FreshGallery(
            itemCount: count,
            empty: const SizedBox(key: Key('noPicture')),
            itemBuilder: (_, i) => ColoredBox(key: ValueKey('picture-$i'), color: Colors.teal),
          ),
        ),
      ),
    ),
  );
}

void main() {
  testWidgets('no picture shows the empty widget', (tester) async {
    await _pump(tester, 0);

    expect(find.byKey(const Key('noPicture')), findsOneWidget);
    expect(find.byKey(const Key('freshGalleryDots')), findsNothing);
  });

  testWidgets('one picture shows alone, without dots or paging', (tester) async {
    await _pump(tester, 1);

    expect(find.byKey(const Key('freshGallerySingle')), findsOneWidget);
    expect(find.byKey(const ValueKey('picture-0')), findsOneWidget);
    expect(find.byType(PageView), findsNothing);
    expect(find.byType(PageDots), findsNothing);
  });

  for (final count in [2, 3, 4]) {
    testWidgets('$count pictures swipe one by one and the dots follow', (tester) async {
      await _pump(tester, count);

      expect(find.byKey(const Key('freshGalleryDots')), findsOneWidget);
      expect(tester.widget<PageDots>(find.byType(PageDots)).count, count);
      expect(tester.widget<PageDots>(find.byType(PageDots)).index, 0);

      for (var i = 1; i < count; i++) {
        await tester.drag(find.byKey(const Key('freshGalleryPages')), const Offset(-300, 0));
        await tester.pumpAndSettle();
        expect(tester.widget<PageDots>(find.byType(PageDots)).index, i);
        expect(find.byKey(ValueKey('picture-$i')), findsOneWidget);
      }

      // The last picture stays the last one.
      await tester.drag(find.byKey(const Key('freshGalleryPages')), const Offset(-300, 0));
      await tester.pumpAndSettle();
      expect(tester.widget<PageDots>(find.byType(PageDots)).index, count - 1);
    });
  }

  testWidgets('screen readers can move with increase and decrease (no swipe needed)', (tester) async {
    await _pump(tester, 3);

    Semantics gallery() => tester.widget<Semantics>(find.byKey(const Key('freshGallery')));
    expect(gallery().properties.onDecrease, isNull); // first picture
    gallery().properties.onIncrease!();
    await tester.pumpAndSettle();
    expect(tester.widget<PageDots>(find.byType(PageDots)).index, 1);

    gallery().properties.onDecrease!();
    await tester.pumpAndSettle();
    expect(tester.widget<PageDots>(find.byType(PageDots)).index, 0);
  });
}
