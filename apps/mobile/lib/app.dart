import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';

import 'router.dart';
import 'state/core_providers.dart';
import 'widgets/states.dart';

final themeModeProvider = NotifierProvider<ThemeModeController, ThemeMode>(ThemeModeController.new);

class ThemeModeController extends Notifier<ThemeMode> {
  @override
  ThemeMode build() => switch (ref.read(appPrefsProvider).themeMode) {
    'light' => ThemeMode.light,
    'dark' => ThemeMode.dark,
    _ => ThemeMode.system,
  };

  Future<void> set(ThemeMode mode) async {
    await ref.read(appPrefsProvider).setThemeMode(mode.name);
    state = mode;
  }
}

class FreshOrbitApp extends ConsumerWidget {
  const FreshOrbitApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (ref.watch(apiBaseUrlProvider).isEmpty) {
      return MaterialApp(
        title: 'Fresh Orbit',
        theme: FreshTheme.light(),
        darkTheme: FreshTheme.dark(),
        home: const Scaffold(
          body: SafeArea(
            child: EmptyState(
              icon: Icons.settings_outlined,
              title: 'API address not configured',
              message: 'Build the app with --dart-define=API_BASE_URL=https://your-api-host.',
            ),
          ),
        ),
      );
    }
    return MaterialApp.router(
      title: 'Fresh Orbit',
      debugShowCheckedModeBanner: false,
      theme: FreshTheme.light(),
      darkTheme: FreshTheme.dark(),
      themeMode: ref.watch(themeModeProvider),
      routerConfig: ref.watch(routerProvider),
    );
  }
}
