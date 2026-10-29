import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';

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

/// Languages whose screen texts exist (D312, MA-8). Only English today, so the language choice is hidden: a picker
/// that offers Telugu or Hindi and then shows English is a broken promise. Add a code here in the same change that
/// adds its translations (flutter_localizations + ARB files), and the choice comes back in Settings.
const availableLanguages = {'en'};

/// The app language code. A stored choice without translations (picked in an earlier version) reads as English.
final languageProvider = NotifierProvider<LanguageController, String>(LanguageController.new);

class LanguageController extends Notifier<String> {
  @override
  String build() {
    final stored = ref.read(appPrefsProvider).language;
    return availableLanguages.contains(stored) ? stored : 'en';
  }

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
        scrollBehavior: const FreshScrollBehavior(),
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
      // Pages stand still at their ends on every platform (D182).
      scrollBehavior: const FreshScrollBehavior(),
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: ref.watch(themeModeProvider),
      routerConfig: ref.watch(routerProvider),
    );
  }
}
