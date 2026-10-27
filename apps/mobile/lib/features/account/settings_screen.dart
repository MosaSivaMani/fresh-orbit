// Settings (D88): notification toggles (the server's preferences), language, theme, privacy and log out. Works
// signed out for language and theme. Only English copy exists so far: Telugu and Hindi are remembered and the
// screen says so.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/account.dart';
import '../../app.dart';
import '../../core/format.dart';
import '../../state/account_state.dart';
import '../../state/session.dart';
import '../../widgets/states.dart';

const languageNames = {'en': 'EN', 'te': 'తెలుగు', 'hi': 'हिं'};
const languageLongNames = {'en': 'English', 'te': 'తెలుగు (Telugu)', 'hi': 'हिंदी (Hindi)'};

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    final language = ref.watch(languageProvider);
    final mode = ref.watch(themeModeProvider);
    return Scaffold(
      appBar: const PageHeader(text: 'App', brushed: 'settings'),
      body: SafeArea(
        top: false,
        child: ListView(
          padding: const EdgeInsets.only(bottom: 24),
          children: [
            const MenuLabel('Notifications'),
            if (!signedIn)
              MenuCard(
                children: [
                  MenuRow(
                    icon: Icons.notifications_none_rounded,
                    title: 'Sign in to choose notifications',
                    onTap: () =>
                        context.push(Uri(path: '/login', queryParameters: {'from': '/account/settings'}).toString()),
                  ),
                ],
              )
            else
              AsyncValueView<List<NotificationPreference>>(
                value: ref.watch(preferencesProvider),
                onRetry: () => ref.invalidate(preferencesProvider),
                loading: const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  child: LoadingSemantics(child: SkeletonBox(height: 120, radius: 18)),
                ),
                data: (list) => MenuCard(
                  children: [
                    if (list.isEmpty)
                      const MenuRow(icon: Icons.notifications_none_rounded, title: 'No preferences to set'),
                    for (final p in list)
                      MenuRow(
                        icon: _iconFor(p.category),
                        title: humanizeCode(p.category),
                        subtitle: humanizeCode(p.channel),
                        trailing: Switch(
                          value: p.enabled,
                          onChanged: (v) =>
                              ref.read(preferencesProvider.notifier).setEnabled(p, v).catchError((Object e) {
                                // PREFERENCE_REQUIRED: order updates by SMS cannot be switched off.
                                if (context.mounted) showErrorSnack(context, e);
                              }),
                        ),
                      ),
                  ],
                ),
              ),
            const MenuLabel('App'),
            MenuCard(
              children: [
                MenuRow(
                  icon: Icons.language_rounded,
                  title: 'Language',
                  subtitle: language == 'en' ? null : 'Coming soon. The app stays in English for now.',
                  trailing: SegmentedPills<String>(
                    key: const Key('languagePicker'),
                    semanticLabel: 'Language',
                    options: languageNames,
                    selected: language,
                    onSelected: (code) => ref.read(languageProvider.notifier).set(code),
                  ),
                ),
                MenuRow(
                  icon: Icons.dark_mode_outlined,
                  title: 'Theme',
                  trailing: SegmentedPills<ThemeMode>(
                    key: const Key('themePicker'),
                    semanticLabel: 'Theme',
                    options: const {ThemeMode.light: 'Light', ThemeMode.system: 'Auto', ThemeMode.dark: 'Dark'},
                    selected: mode,
                    onSelected: (m) => ref.read(themeModeProvider.notifier).set(m),
                  ),
                ),
                MenuRow(
                  icon: Icons.description_outlined,
                  title: 'Privacy and terms',
                  onTap: () => context.go('/account/help'),
                ),
                if (signedIn)
                  MenuRow(
                    key: const Key('logOut'),
                    icon: Icons.logout_rounded,
                    title: 'Log out',
                    danger: true,
                    trailing: const SizedBox.shrink(),
                    onTap: () async {
                      await ref.read(sessionProvider.notifier).signOut();
                      if (context.mounted) showInfoSnack(context, 'You are logged out.');
                    },
                  ),
              ],
            ),
            if (signedIn)
              Padding(
                padding: const EdgeInsets.only(top: 10),
                child: Center(
                  child: TextButton(
                    style: TextButton.styleFrom(foregroundColor: c.muted),
                    onPressed: () => context.go('/account/profile'),
                    child: const Text('Delete my account'),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  static IconData _iconFor(String category) => switch (category) {
    'ORDER_UPDATES' || 'ORDER' => Icons.notifications_none_rounded,
    'OFFERS' || 'MARKETING' || 'PROMOTIONS' => Icons.local_offer_outlined,
    _ => Icons.sms_outlined,
  };
}
