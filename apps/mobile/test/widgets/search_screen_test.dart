import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/api/models/common.dart';
import 'package:fresh_orbit_app/core/search_text.dart';
import 'package:fresh_orbit_app/features/catalog/search_screen.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';

import '../support/harness.dart';

void main() {
  late TestDeps deps;
  late List<ProductQuery> searched;
  late List<String> suggested;

  setUp(() {
    IllustrationAssets.debugSetAvailable(const {});
    deps = TestDeps(pincode: '600041');
    searched = [];
    suggested = [];
    deps.catalog.onProducts = (q) async {
      searched.add(q);
      return const Paged<ProductSummary>(items: <ProductSummary>[], page: 1, pageSize: 20, totalCount: 0);
    };
    deps.catalog.onSuggest = (q) async {
      suggested.add(q);
      return const <SearchSuggestion>[];
    };
  });
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  Future<void> search(WidgetTester tester, String text) async {
    await tester.enterText(find.byKey(const Key('searchField')), text);
    // Past the suggestion debounce.
    await tester.pump(const Duration(milliseconds: 300));
    await tester.pumpAndSettle();
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
  }

  for (final text in ['a', '%%', '  - ']) {
    testWidgets('"$text" is never sent to products or suggest; the hint stays (D235)', (tester) async {
      await pumpScreen(tester, deps, const SearchScreen());
      await search(tester, text);

      expect(searched, isEmpty);
      expect(suggested, isEmpty);
      expect(find.text('Search the catch'), findsOneWidget);
    });
  }

  testWidgets('a valid search is sent trimmed', (tester) async {
    await pumpScreen(tester, deps, const SearchScreen());
    await search(tester, '  seer ');

    expect(suggested, ['seer']);
    expect(searched.single.q, 'seer');
  });

  testWidgets('the field stops at the longest search the server accepts', (tester) async {
    await pumpScreen(tester, deps, const SearchScreen());
    await tester.enterText(find.byKey(const Key('searchField')), 'a' * 80);
    await tester.pump();

    expect(tester.widget<TextField>(find.byKey(const Key('searchField'))).controller!.text, hasLength(searchMaxLength));
  });
}
