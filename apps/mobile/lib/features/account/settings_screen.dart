// Settings (D88): this phone's notification permission (D352), notification toggles (the server's preferences), language, theme, log out, and the policy pages
// on the website (D299). Works signed out for language and theme. Only English copy exists so far, so the language
// choice is hidden until a second language has its translations (D312, MA-8).

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/account.dart';
import '../../app.dart';
import '../../core/format.dart';
import '../../core/push_service.dart';
import '../../state/account_state.dart';
import '../../state/core_providers.dart';
import '../../state/push_state.dart';
import '../../state/session.dart';
import '../../widgets/policy_links.dart';
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
                    onTap: () => context.push('/login'),
                  ),
                ],
              )
            else ...[
              // This phone's permission (D352): shown only when the build has push (D350).
              if (ref.watch(pushServiceProvider).available) ...[
                const _PhonePushCard(),
                const SizedBox(height: 10),
              ],
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
            ],
            const MenuLabel('App'),
            MenuCard(
              children: [
                // Shown only once a second language has its translations (D312).
                if (availableLanguages.length > 1)
                  MenuRow(
                    icon: Icons.language_rounded,
                    title: 'Language',
                    trailing: SegmentedPills<String>(
                      key: const Key('languagePicker'),
                      semanticLabel: 'Language',
                      options: {
                        for (final e in languageNames.entries)
                          if (availableLanguages.contains(e.key)) e.key: e.value,
                      },
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
            // The policy pages on the website (D299); left out when the website address is not configured.
            if (ref.watch(policyLinksProvider).isNotEmpty) ...[
              const MenuLabel('Privacy and policies'),
              const PolicyMenuCard(key: Key('settingsPolicies')),
            ],
            if (signedIn)
              Padding(
                padding: const EdgeInsets.only(top: 10),
                child: Center(
                  child: TextButton(
                    style: TextButton.styleFrom(foregroundColor: c.muted),
                    onPressed: () => context.push('/account/profile'),
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

/// "Notifications on this phone" (D352): the phone's permission for this app. Off offers "Turn on", which asks the
/// system question; when the phone no longer asks (refused before), the customer is told where to allow it.
class _PhonePushCard extends ConsumerWidget {
  const _PhonePushCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final on = ref.watch(pushPermissionProvider).value == PushPermission.granted;
    return MenuCard(
      children: [
        MenuRow(
          key: const Key('phonePush'),
          icon: on ? Icons.notifications_active_outlined : Icons.notifications_off_outlined,
          title: 'Notifications on this phone',
          subtitle: on ? 'On' : 'Off',
          trailing: on
              ? const SizedBox.shrink()
              : TextButton(
                  key: const Key('phonePushTurnOn'),
                  onPressed: () async {
                    final answer = await ref.read(pushCoordinatorProvider).askFromSettings();
                    if (answer != PushPermission.granted && context.mounted) {
                      showInfoSnack(context, 'Allow notifications for Fresh Orbitt in your phone settings.');
                    }
                  },
                  child: const Text('Turn on'),
                ),
        ),
      ],
    );
  }
}
