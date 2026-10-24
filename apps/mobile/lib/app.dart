import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'router.dart';
import 'state/core_providers.dart';
import 'theme/app_theme.dart';
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

/// The chosen app language code ('en', 'te', 'hi'). Screens are English only for now (docs/design/mobile.md).
final languageProvider = NotifierProvider<LanguageController, String>(LanguageController.new);

class LanguageController extends Notifier<String> {
  @override
  String build() => ref.read(appPrefsProvider).language;

  Future<void> set(String code) async {
    await ref.read(appPrefsProvider).setLanguage(code);
    state = code;
  }
}

class FreshOrbitApp extends ConsumerWidget {
  const FreshOrbitApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (ref.watch(apiBaseUrlProvider).isEmpty) {
      return MaterialApp(
        title: 'Fresh Orbitt',
        theme: AppTheme.light(),
        darkTheme: AppTheme.dark(),
        home: const Scaffold(
          body: SafeArea(
            child: EmptyState(
              slot: IllustrationSlot.errorGeneric,
              title: 'API address not configured',
              message: 'Build the app with --dart-define=API_BASE_URL=https://your-api-host.',
            ),
          ),
        ),
      );
    }
    return MaterialApp.router(
      title: 'Fresh Orbitt',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: ref.watch(themeModeProvider),
      routerConfig: ref.watch(routerProvider),
    );
  }
}
