import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/router.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:go_router/go_router.dart';

import '../support/harness.dart';

Widget _marker(GoRouterState state) => Scaffold(body: Text('route:${state.uri}'));

/// The real AppShell over five marker tabs; the Account tab has one inner page.
Future<GoRouter> _pumpShell(WidgetTester tester, String initialLocation) async {
  final deps = TestDeps();
  await deps.load();
  final router = GoRouter(
    initialLocation: initialLocation,
    routes: [
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => AppShell(shell: shell),
        branches: [
          for (final path in ['/home', '/shop', '/cart', '/orders'])
            StatefulShellBranch(routes: [GoRoute(path: path, builder: (_, s) => _marker(s))]),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/account',
                builder: (_, s) => _marker(s),
                routes: [GoRoute(path: 'help', builder: (_, s) => _marker(s))],
              ),
            ],
          ),
        ],
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: deps.overrides,
      retry: (_, _) => null,
      child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
  return router;
}

Future<void> _back(WidgetTester tester) async {
  await tester.binding.handlePopRoute();
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('back on another tab goes to Home instead of closing the app', (tester) async {
    final router = await _pumpShell(tester, '/shop');

    await _back(tester);

    expect(router.state.uri.toString(), '/home');
    expect(find.text('route:/home'), findsOneWidget);
  });

  testWidgets('back on a page inside a tab returns to the tab first page', (tester) async {
    final router = await _pumpShell(tester, '/account/help');

    await _back(tester);
    expect(router.state.uri.toString(), '/account');

    await _back(tester);
    expect(router.state.uri.toString(), '/home');
  });

  testWidgets('the first back on Home asks for a second one and stays open', (tester) async {
    final router = await _pumpShell(tester, '/home');

    await tester.binding.handlePopRoute();
    await tester.pump();

    expect(find.byKey(const ValueKey('pressBackToExit')), findsOneWidget);
    expect(router.state.uri.toString(), '/home');
  });
}
