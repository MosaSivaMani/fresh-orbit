import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/token_store.dart';
import 'package:fresh_orbit_app/core/app_prefs.dart';
import 'package:fresh_orbit_app/state/core_providers.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:go_router/go_router.dart';

import 'fakes.dart';

/// Fakes for every repository, shared by a test and the widget under test.
class TestDeps {
  TestDeps({String? pincode, bool signedIn = false})
    : prefs = MemoryAppPrefs(pincode: pincode, onboardingDone: true),
      tokens = TokenStore(MemorySecretStore(signedIn ? {'fo.refreshToken': 'r1'} : null));

  final MemoryAppPrefs prefs;
  final TokenStore tokens;
  final location = FakeLocationRepository();
  final catalog = FakeCatalogRepository();
  final cart = FakeCartRepository();
  final orders = FakeOrderRepository();
  final account = FakeAccountRepository();
  final auth = FakeAuthRepository();

  List<Override> get overrides => [
    appPrefsProvider.overrideWithValue(prefs),
    tokenStoreProvider.overrideWithValue(tokens),
    apiBaseUrlProvider.overrideWithValue('https://api.test'),
    locationRepositoryProvider.overrideWithValue(location),
    catalogRepositoryProvider.overrideWithValue(catalog),
    cartRepositoryProvider.overrideWithValue(cart),
    orderRepositoryProvider.overrideWithValue(orders),
    accountRepositoryProvider.overrideWithValue(account),
    authRepositoryProvider.overrideWithValue(auth),
  ];

  Future<void> load() => tokens.load();

  ProviderContainer container() => ProviderContainer.test(overrides: overrides, retry: (_, _) => null);
}

/// Pumps [screen] at '/' inside a router whose other paths render a marker text ("route:/home"), so tests can
/// assert navigation without building the real destination.
Future<GoRouter> pumpScreen(
  WidgetTester tester,
  TestDeps deps,
  Widget screen, {
  List<String> otherPaths = const [],
  List<Override> overrides = const [],
}) async {
  // Phone width, tall enough that lazy lists build their content without scrolling in most tests.
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = const Size(400 * 3, 2400 * 3);
  addTearDown(tester.view.reset);
  await deps.load();
  final router = GoRouter(
    routes: [
      GoRoute(path: '/', builder: (_, _) => screen),
      for (final p in {'/home', '/cart', '/checkout', '/login', '/location', ...otherPaths})
        GoRoute(
          path: p,
          builder: (_, state) => Scaffold(body: Text('route:${state.uri}')),
        ),
      GoRoute(
        path: '/order-success/:id',
        builder: (_, state) => Scaffold(body: Text('route:${state.uri}')),
      ),
      GoRoute(
        path: '/product/:slug',
        builder: (_, state) => Scaffold(body: Text('route:${state.uri}')),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: [...deps.overrides, ...overrides],
      retry: (_, _) => null,
      child: MaterialApp.router(theme: AppTheme.light(), routerConfig: router),
    ),
  );
  return router;
}
