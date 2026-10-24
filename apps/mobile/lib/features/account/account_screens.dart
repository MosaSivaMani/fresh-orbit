// Account tab: profile (If-Match), addresses, wishlist, in-app notifications, notification preferences,
// appearance, help, sign out and account deletion.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/account.dart';
import '../../app.dart';
import '../../core/format.dart';
import '../../demo/demo_app.dart';
import '../../demo/demo_backend.dart';
import '../../illustrations/illustration.dart';
import '../../state/account_state.dart';
import '../../state/session.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

class AccountScreen extends ConsumerWidget {
  const AccountScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    final profile = signedIn ? ref.watch(profileProvider).value : null;
    final unread = signedIn ? ref.watch(inboxProvider).value?.unreadCount ?? 0 : 0;
    return Scaffold(
      appBar: AppBar(title: const Text('Account')),
      body: ListView(
        children: [
          if (demoMode) const DemoPanel(),
          if (!signedIn)
            Padding(
              padding: const EdgeInsets.all(FreshSpacing.s4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Center(
                    child: Illustration(slot: IllustrationSlot.loginOtp, size: 140, excludeFromSemantics: true),
                  ),
                  const SizedBox(height: FreshSpacing.s3),
                  Text(
                    'Sign in to see your orders, addresses and wishlist.',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: FreshSpacing.s3),
                  FilledButton(
                    onPressed: () =>
                        context.push(Uri(path: '/login', queryParameters: {'from': '/account'}).toString()),
                    child: const Text('Sign in'),
                  ),
                ],
              ),
            )
          else ...[
            ListTile(
              leading: const CircleAvatar(child: Icon(Icons.person_outline)),
              title: Text(profile?.fullName ?? 'Your profile'),
              subtitle: profile?.phone == null ? null : Text(formatPhone(profile!.phone!)),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.go('/account/profile'),
            ),
            const Divider(),
            const _Link(icon: Icons.location_on_outlined, label: 'Addresses', path: '/account/addresses'),
            const _Link(icon: Icons.favorite_outline, label: 'Wishlist', path: '/wishlist', push: true),
            _Link(
              icon: Icons.notifications_outlined,
              label: 'Notifications',
              path: '/account/notifications',
              badge: unread > 0 ? '$unread' : null,
            ),
            const _Link(icon: Icons.tune, label: 'Notification preferences', path: '/account/preferences'),
          ],
          const _Link(icon: Icons.help_outline, label: 'Help and support', path: '/account/help'),
          const Divider(),
          const _ThemePicker(),
          if (signedIn) ...[
            const Divider(),
            ListTile(
              leading: const Icon(Icons.logout),
              title: const Text('Sign out'),
              onTap: () async {
                await ref.read(sessionProvider.notifier).signOut();
                if (context.mounted) showInfoSnack(context, 'You are signed out.');
              },
            ),
          ],
        ],
      ),
    );
  }
}

class _Link extends StatelessWidget {
  const _Link({required this.icon, required this.label, required this.path, this.badge, this.push = false});

  final IconData icon;
  final String label;
  final String path;
  final String? badge;
  final bool push;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: Icon(icon),
      title: Text(label),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (badge != null) Badge(label: Text(badge!, semanticsLabel: '$badge unread')),
          const Icon(Icons.chevron_right),
        ],
      ),
      onTap: () => push ? context.push(path) : context.go(path),
    );
  }
}

class _ThemePicker extends ConsumerWidget {
  const _ThemePicker();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mode = ref.watch(themeModeProvider);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s4, vertical: FreshSpacing.s2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Appearance', style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(height: FreshSpacing.s2),
          SegmentedButton<ThemeMode>(
            segments: const [
              ButtonSegment(value: ThemeMode.system, label: Text('System')),
              ButtonSegment(value: ThemeMode.light, label: Text('Light')),
              ButtonSegment(value: ThemeMode.dark, label: Text('Dark')),
            ],
            selected: {mode},
            onSelectionChanged: (s) => ref.read(themeModeProvider.notifier).set(s.first),
          ),
        ],
      ),
    );
  }
}

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  final _name = TextEditingController();
  final _email = TextEditingController();
  bool _marketing = false;
  String? _loadedVersion;
  bool _saving = false;
  ApiError? _error;

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    super.dispose();
  }

  void _fill(Profile p) {
    if (_loadedVersion == p.version) return;
    _loadedVersion = p.version;
    _name.text = p.fullName ?? '';
    _email.text = p.email ?? '';
    _marketing = p.marketingOptIn;
  }

  Future<void> _save() async {
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ref
          .read(profileProvider.notifier)
          .save(
            fullName: _name.text.trim(),
            email: _email.text.trim().isEmpty ? null : _email.text.trim(),
            marketingOptIn: _marketing,
          );
      if (mounted) showInfoSnack(context, 'Profile saved.');
    } on ApiError catch (e) {
      if (e.status == 412) {
        _loadedVersion = null;
        ref.invalidate(profileProvider);
      }
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _delete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete your account?'),
        content: const Text(
          'Your profile, addresses and wishlist are removed. Orders stay on record as the law requires. '
          'This cannot be undone.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Keep account')),
          TextButton(
            key: const Key('confirmDelete'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Delete account'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    try {
      await ref.read(profileProvider.notifier).deleteAccount();
      if (mounted) {
        context.go('/home');
        showInfoSnack(context, 'Your account is deleted.');
      }
    } on ApiError catch (e) {
      // 409: refused while an order is still open; the server says why.
      setState(() => _error = e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final profile = ref.watch(profileProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Profile')),
      body: AsyncValueView<Profile?>(
        value: profile,
        onRetry: () => ref.invalidate(profileProvider),
        data: (p) {
          if (p == null) return const EmptyState(slot: IllustrationSlot.loginOtp, title: 'Sign in to see your profile');
          _fill(p);
          return ListView(
            padding: const EdgeInsets.all(FreshSpacing.s4),
            children: [
              if (p.phone != null)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.phone_outlined),
                  title: Text(formatPhone(p.phone!)),
                  subtitle: const Text('Your sign-in number'),
                ),
              TextField(
                controller: _name,
                textCapitalization: TextCapitalization.words,
                decoration: InputDecoration(labelText: 'Full name', errorText: _error?.fieldMessage('fullName')),
              ),
              const SizedBox(height: FreshSpacing.s3),
              TextField(
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                decoration: InputDecoration(labelText: 'E-mail (optional)', errorText: _error?.fieldMessage('email')),
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                value: _marketing,
                onChanged: (v) => setState(() => _marketing = v),
                title: const Text('Offers and news'),
                subtitle: const Text('Send me offers by SMS and e-mail.'),
              ),
              if (_error != null && _error!.errors.isEmpty) InfoBanner(tone: BannerTone.danger, text: _error!.message),
              const SizedBox(height: FreshSpacing.s3),
              PrimaryButton(label: 'Save', loading: _saving, onPressed: _save),
              const SizedBox(height: FreshSpacing.s6),
              TextButton.icon(
                style: TextButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
                icon: const Icon(Icons.delete_outline),
                label: const Text('Delete my account'),
                onPressed: _delete,
              ),
            ],
          );
        },
      ),
    );
  }
}

class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final inbox = ref.watch(inboxProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          if ((inbox.value?.unreadCount ?? 0) > 0)
            TextButton(
              onPressed: () => ref.read(inboxProvider.notifier).markAllRead().catchError((Object e) {
                if (context.mounted) showErrorSnack(context, e);
              }),
              child: const Text('Mark all read'),
            ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(inboxProvider.future),
        child: AsyncValueView<InboxPage>(
          value: inbox,
          onRetry: () => ref.invalidate(inboxProvider),
          data: (page) => page.items.isEmpty
              ? const EmptyState(slot: IllustrationSlot.emptyNotifications, title: 'No notifications yet')
              : ListView.separated(
                  itemCount: page.items.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, i) {
                    final m = page.items[i];
                    return ListTile(
                      leading: Icon(m.isRead ? Icons.mark_email_read_outlined : Icons.mark_email_unread_outlined),
                      title: Text(
                        m.title ?? m.body,
                        style: m.isRead ? null : const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      subtitle: Text(
                        m.title == null ? formatDateTime(m.createdAt) : '${m.body}\n${formatDateTime(m.createdAt)}',
                      ),
                      isThreeLine: m.title != null,
                      onTap: m.isRead
                          ? null
                          : () => ref.read(inboxProvider.notifier).markRead(m.messageId).catchError((Object e) {
                              if (context.mounted) showErrorSnack(context, e);
                            }),
                    );
                  },
                ),
        ),
      ),
    );
  }
}

class PreferencesScreen extends ConsumerWidget {
  const PreferencesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final prefs = ref.watch(preferencesProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Notification preferences')),
      body: AsyncValueView<List<NotificationPreference>>(
        value: prefs,
        onRetry: () => ref.invalidate(preferencesProvider),
        data: (list) => list.isEmpty
            ? const EmptyState(slot: IllustrationSlot.emptyNotifications, title: 'No preferences to set')
            : ListView(
                children: [
                  for (final p in list)
                    SwitchListTile(
                      value: p.enabled,
                      title: Text(humanizeCode(p.category)),
                      subtitle: Text(humanizeCode(p.channel)),
                      onChanged: (v) => ref.read(preferencesProvider.notifier).setEnabled(p, v).catchError((Object e) {
                        // PREFERENCE_REQUIRED: order updates by SMS cannot be switched off.
                        if (context.mounted) showErrorSnack(context, e);
                      }),
                    ),
                ],
              ),
      ),
    );
  }
}
