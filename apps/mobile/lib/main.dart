import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api/token_store.dart';
import 'app.dart';
import 'core/app_prefs.dart';
import 'state/core_providers.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
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
