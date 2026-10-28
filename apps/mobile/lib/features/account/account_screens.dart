// Account tab: profile (If-Match), addresses, wishlist, in-app notifications, notification preferences,
// help and account deletion. Theme, language and log out live in Settings (settings_screen.dart).

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
import '../../state/account_state.dart';
import '../../state/session.dart';
import '../../widgets/bold.dart';
import '../../widgets/states.dart';
import 'settings_screen.dart';

class AccountScreen extends ConsumerWidget {
  const AccountScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    final profile = signedIn ? ref.watch(profileProvider).value : null;
    final unread = signedIn ? ref.watch(inboxProvider).value?.unreadCount ?? 0 : 0;
    final language = ref.watch(languageProvider);
    return Scaffold(
      appBar: PageHeader(
        text: 'My',
        brushed: 'account',
        back: false,
        actions: [
          PaperCircleButton(
            key: const Key('accountSettings'),
            icon: Icons.settings_outlined,
            label: 'Settings',
            onTap: () => context.go('/account/settings'),
          ),
        ],
      ),
      body: SafeArea(
        top: false,
        child: ListView(
          padding: const EdgeInsets.only(bottom: FreshLayout.sectionGap),
          children: [
            if (demoMode) const DemoPanel(),
            _ProfileCard(signedIn: signedIn, profile: profile),
            if (signedIn)
              Padding(
                padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, FreshLayout.cardGap, FreshLayout.gutter, 0),
                child: FreshColumns(
                  stretch: true,
                  children: [
                    _QuickTile(
                      icon: Icons.receipt_long_outlined,
                      label: 'Orders',
                      tint: c.tealSoft,
                      onTap: () => context.go('/orders'),
                    ),
                    _QuickTile(
                      icon: Icons.favorite_border_rounded,
                      label: 'Saved',
                      tint: c.coralSoft,
                      onTap: () => context.push('/wishlist'),
                    ),
                    _QuickTile(
                      icon: Icons.location_on_outlined,
                      label: 'Addresses',
                      tint: c.iceSoft,
                      onTap: () => context.go('/account/addresses'),
                    ),
                  ],
                ),
              ),
            const SizedBox(height: FreshLayout.cardGap),
            MenuCard(
              children: [
                if (signedIn)
                  MenuRow(
                    icon: Icons.notifications_none_rounded,
                    title: 'Notifications',
                    subtitle: 'Order updates, offers',
                    trailing: unread > 0
                        ? Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Badge(label: Text('$unread', semanticsLabel: '$unread unread')),
                              Icon(Icons.chevron_right_rounded, size: 18, color: c.muted),
                            ],
                          )
                        : null,
                    onTap: () => context.go('/account/notifications'),
                  ),
                MenuRow(
                  icon: Icons.language_rounded,
                  title: 'Language',
                  subtitle: languageLongNames[language] ?? language,
                  onTap: () => context.go('/account/settings'),
                ),
                MenuRow(
                  icon: Icons.help_outline_rounded,
                  title: 'Help and support',
                  subtitle: 'Questions about an order',
                  onTap: () => context.go('/account/help'),
                ),
                MenuRow(icon: Icons.settings_outlined, title: 'Settings', onTap: () => context.go('/account/settings')),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// The mango profile block: initial, name, phone and Edit; signed out it invites to sign in.
class _ProfileCard extends StatelessWidget {
  const _ProfileCard({required this.signedIn, required this.profile});

  final bool signedIn;
  final Profile? profile;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final name = profile?.fullName;
    final initial = name == null || name.trim().isEmpty ? null : name.trim()[0].toUpperCase();
    return Container(
      margin: FreshLayout.page,
      padding: const EdgeInsets.all(FreshLayout.gutter),
      decoration: BoxDecoration(color: c.mango, borderRadius: BorderRadius.circular(AppRadius.hero)),
      child: signedIn
          ? Row(
              children: [
                Container(
                  width: 62,
                  height: 62,
                  decoration: BoxDecoration(
                    color: BlockInk.paper,
                    shape: BoxShape.circle,
                    border: Border.all(color: BlockInk.ink, width: 3),
                  ),
                  alignment: Alignment.center,
                  child: initial == null
                      ? const Icon(Icons.person_outline_rounded, color: BlockInk.ink, size: 28)
                      : ExcludeSemantics(child: Text(initial, style: AppText.display(BlockInk.ink, 26))),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(name ?? 'Your profile', style: AppText.display(BlockInk.ink, 21)),
                      if (profile?.phone != null)
                        Text(
                          formatPhone(profile!.phone!),
                          style: const TextStyle(
                            fontFamily: AppFonts.body,
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: BlockInk.ink,
                          ),
                        ),
                    ],
                  ),
                ),
                Semantics(
                  button: true,
                  label: 'Edit profile',
                  excludeSemantics: true,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(999),
                    onTap: () => context.go('/account/profile'),
                    child: Container(
                      constraints: const BoxConstraints(minHeight: 32),
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                      decoration: BoxDecoration(
                        color: BlockInk.paper,
                        borderRadius: BorderRadius.circular(999),
                        border: Border.all(color: BlockInk.ink, width: 1.5),
                      ),
                      alignment: Alignment.center,
                      child: const Text(
                        'Edit',
                        style: TextStyle(
                          fontFamily: AppFonts.body,
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                          color: BlockInk.ink,
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            )
          : Row(
              children: [
                const AvatarCircle(slot: IllustrationSlot.loginOtp, size: 76),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Sign in to see your orders', style: AppText.display(BlockInk.ink, 18)),
                      const SizedBox(height: 6),
                      FilledButton(
                        style: FilledButton.styleFrom(
                          backgroundColor: BlockInk.ink,
                          foregroundColor: BlockInk.paper,
                          minimumSize: const Size(48, 44),
                        ),
                        onPressed: () => context.push('/login'),
                        child: const Text('Sign in'),
                      ),
                    ],
                  ),
                ),
              ],
            ),
    );
  }
}

class _QuickTile extends StatelessWidget {
  const _QuickTile({required this.icon, required this.label, required this.tint, required this.onTap});

  final IconData icon;
  final String label;
  final Color tint;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: FreshCard(
        color: c.paper,
        shadow: cardShadow(context),
        onTap: onTap,
        padding: const EdgeInsets.symmetric(vertical: FreshLayout.cardGap, horizontal: 4),
        child: Column(
                  children: [
                    Container(
                      width: 34,
                      height: 34,
                      decoration: BoxDecoration(color: tint, borderRadius: BorderRadius.circular(11)),
                      child: Icon(icon, size: 18, color: c.ink),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      label,
                      style: TextStyle(
                        fontFamily: AppFonts.body,
                        fontSize: 11.5,
                        fontWeight: FontWeight.w700,
                        color: c.ink,
                      ),
                    ),
                  ],
                ),
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
      // 409 while an order is still open, 422 ACCOUNT_HAS_OPEN_MATTERS (open complaint or refund), 412 when the
      // profile changed: the server says why. A stale version is read again for the next try.
      if (e.status == 412) {
        _loadedVersion = null;
        ref.invalidate(profileProvider);
      }
      if (mounted) setState(() => _error = e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final profile = ref.watch(profileProvider);
    return Scaffold(
      appBar: const PageHeader(text: 'Your', brushed: 'profile'),
      body: AsyncValueView<Profile?>(
        value: profile,
        onRetry: () => ref.invalidate(profileProvider),
        data: (p) {
          if (p == null) return const EmptyState(slot: IllustrationSlot.loginOtp, title: 'Sign in to see your profile');
          _fill(p);
          return ListView(
            padding: FreshLayout.list,
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
              // Anything not shown at a field: 409 open orders, 422 ACCOUNT_HAS_OPEN_MATTERS (errors.account), 429.
              if (_error != null && _error!.fieldMessage('fullName') == null && _error!.fieldMessage('email') == null)
                InfoBanner(key: const Key('profileError'), tone: BannerTone.danger, text: _error!.message),
              const SizedBox(height: FreshSpacing.s3),
              PrimaryButton(label: 'Save', loading: _saving, onPressed: _save),
              const SizedBox(height: FreshSpacing.s6),
              TextButton.icon(
                style: TextButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
                icon: const Icon(Icons.delete_outline),
                key: const Key('deleteAccount'),
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
    final empty = inbox.value?.items.isEmpty ?? false;
    return Scaffold(
      backgroundColor: empty ? BlockTone.ice.background(illustratedColors(context)) : null,
      appBar: empty
          ? null
          : PageHeader(
              text: 'Your',
              brushed: 'alerts',
              actions: [
                if ((inbox.value?.unreadCount ?? 0) > 0)
                  PaperCircleButton(
                    key: const Key('markAllRead'),
                    icon: Icons.done_all_rounded,
                    label: 'Mark all read',
                    onTap: () => ref.read(inboxProvider.notifier).markAllRead().catchError((Object e) {
                      if (context.mounted) showErrorSnack(context, e);
                    }),
                  ),
              ],
            ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(inboxProvider.future),
        child: AsyncValueView<InboxPage>(
          value: inbox,
          onRetry: () => ref.invalidate(inboxProvider),
          data: (page) => page.items.isEmpty
              ? _NoNotifications(onBack: context.canPop() ? () => context.pop() : null)
              : ListView.separated(
                  padding: FreshLayout.list,
                  itemCount: page.items.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 8),
                  itemBuilder: (context, i) {
                    final m = page.items[i];
                    return SectionBox(
                      padding: EdgeInsets.zero,
                      child: ListTile(
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
                      ),
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
      appBar: const PageHeader(text: 'Message', brushed: 'choices'),
      body: AsyncValueView<List<NotificationPreference>>(
        value: prefs,
        onRetry: () => ref.invalidate(preferencesProvider),
        data: (list) => list.isEmpty
            ? const EmptyState(slot: IllustrationSlot.emptyNotifications, title: 'No preferences to set')
            : ListView(
                padding: FreshLayout.list,
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

/// No notifications: the ice block with Chanduva resting.
class _NoNotifications extends StatelessWidget {
  const _NoNotifications({this.onBack});

  final VoidCallback? onBack;

  @override
  Widget build(BuildContext context) => ColorBlockScreen(
    tone: BlockTone.ice,
    eyebrow: 'Notifications',
    heading: const [HeadingLine('All', script: true), HeadingLine('quiet', brush: true)],
    slot: IllustrationSlot.emptyNotifications,
    onBack: onBack,
    children: const [
      SheetTitle('Nothing new', header: true),
      SheetNote('We’ll tell you when your order is packed, on the way and delivered.'),
    ],
  );
}
