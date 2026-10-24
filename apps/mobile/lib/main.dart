import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api/token_store.dart';
import 'app.dart';
import 'core/app_prefs.dart';
import 'demo/demo_app.dart';
import 'demo/demo_backend.dart';
import 'state/core_providers.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Demo build: fixture data, no server. `demoMode` is a compile-time constant, so normal builds drop the demo
  // code entirely; a store (Android/iOS release) build refuses it.
  if (demoMode) {
    if (kReleaseMode && !kIsWeb) {
      throw StateError('DEMO=true is only for the web preview. Build store releases without it.');
    }
    return runDemoApp();
  }
  final prefs = await SharedAppPrefs.load();
  final tokens = TokenStore(SecureSecretStore());
  await tokens.load();
  runApp(
    ProviderScope(
      overrides: [appPrefsProvider.overrideWithValue(prefs), tokenStoreProvider.overrideWithValue(tokens)],
      // Screens show an error with a "Try again" button instead of silent automatic retries.
      retry: (_, _) => null,
      child: const FreshOrbitApp(),
    ),
  );
}
