// "Tell us about you" (D290): asked once after a sign-in whose account has no name (a new number). Name is
// required, e-mail optional; both go to the existing profile update, PATCH /me with If-Match (CUS-R2), and the
// server's field errors show at the fields. Saving pops the step, so the customer lands where the sign-in was
// taking them (the first-run destination of D278, or the page that needed sign-in such as /checkout). Back pops
// it without saving. The router pushes it (state/first_run.dart, askAboutYouAfterSignIn); the sign-in screen
// itself is unchanged.
//
// Look: the sign-in family (D281) — FreshAuthPage with "Tell us" / "about you" on the mango brush, Ammamma in
// the ringed avatar, FreshTextField fields on the cream card and the dark "Continue" pill.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/account.dart';
import '../../illustrations/illustration.dart';
import '../../illustrations/portrait_painter.dart';
import '../../state/account_state.dart';

class ProfileDetailsScreen extends ConsumerStatefulWidget {
  const ProfileDetailsScreen({super.key});

  @override
  ConsumerState<ProfileDetailsScreen> createState() => _ProfileDetailsScreenState();
}

class _ProfileDetailsScreenState extends ConsumerState<ProfileDetailsScreen> {
  final _name = TextEditingController();
  final _email = TextEditingController();
  String? _filledVersion;
  String? _nameError;
  ApiError? _error;
  bool _saving = false;

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    super.dispose();
  }

  /// Fills the fields once per profile version (a re-read after 412 keeps what the customer typed when empty).
  void _fill(Profile p) {
    if (_filledVersion == p.version) return;
    _filledVersion = p.version;
    if (_name.text.isEmpty) _name.text = p.fullName ?? '';
    if (_email.text.isEmpty) _email.text = p.email ?? '';
  }

  Future<void> _save() async {
    final name = _name.text.trim();
    final email = _email.text.trim();
    if (name.isEmpty) {
      setState(() {
        _nameError = 'Enter your name.';
        _error = null;
      });
      return;
    }
    setState(() {
      _saving = true;
      _nameError = null;
      _error = null;
    });
    try {
      // Empty e-mail is not sent (null leaves it unchanged, CUS-R2); the server checks both values.
      await ref.read(profileProvider.notifier).save(fullName: name, email: email.isEmpty ? null : email);
      if (mounted) _leave();
    } on ApiError catch (e) {
      // A stale or lost-race version (412, 409 CONCURRENT_SAVE): read the profile again for the next try.
      if (e.status == 412 || e.code == 'CONCURRENT_SAVE') ref.invalidate(profileProvider);
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  /// Back to the page the sign-in landed on (the step was pushed over it); alone, home.
  void _leave() {
    if (context.canPop()) {
      context.pop();
    } else {
      context.go('/home');
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final profile = ref.watch(profileProvider);
    final p = profile.value;
    if (p != null) _fill(p);
    final nameError = _nameError ?? _error?.fieldMessage('fullName');
    final emailError = _error?.fieldMessage('email');
    return Scaffold(
      backgroundColor: c.paper,
      body: AutofillGroup(
        child: FreshAuthPage(
          eyebrow: 'Welcome',
          scriptLine: 'Tell us',
          brushLine: 'about you',
          avatar: LayoutBuilder(
            builder: (context, box) => Illustration(
              slot: IllustrationSlot.loginOtp,
              size: box.maxWidth,
              inCircle: true,
              excludeFromSemantics: true,
              fallback: AmmammaPortrait(size: box.maxWidth),
            ),
          ),
          onBack: context.canPop() ? () => context.pop() : null,
          children: [
            const SheetNote('We use your name on your orders and deliveries. E-mail is optional.'),
            if (p == null && profile.hasError && !profile.isLoading) ...[
              InfoBanner(
                key: const Key('aboutYouLoadError'),
                tone: BannerTone.danger,
                text: ApiError.from(profile.error!).message,
              ),
              TextButton(onPressed: () => ref.invalidate(profileProvider), child: const Text('Try again')),
            ] else if (p == null)
              const Center(child: CircularProgressIndicator(semanticsLabel: 'Loading'))
            else ...[
              FreshTextField(
                fieldKey: const Key('aboutYouName'),
                controller: _name,
                label: 'Name',
                hint: 'Your full name',
                errorText: nameError,
                textCapitalization: TextCapitalization.words,
                textInputAction: TextInputAction.next,
                autofillHints: const [AutofillHints.name],
                // The server takes 1 to 100 characters after trimming (CUS-R2).
                inputFormatters: [LengthLimitingTextInputFormatter(100)],
              ),
              FreshTextField(
                fieldKey: const Key('aboutYouEmail'),
                controller: _email,
                label: 'E-mail (optional)',
                hint: 'you@example.com',
                errorText: emailError,
                keyboardType: TextInputType.emailAddress,
                textInputAction: TextInputAction.done,
                autofillHints: const [AutofillHints.email],
                inputFormatters: [LengthLimitingTextInputFormatter(254)],
                onSubmitted: (_) => _save(),
              ),
              // Anything not shown at a field: 412 / 409 (read again), 429, a network failure.
              if (_error != null && nameError == null && emailError == null)
                InfoBanner(key: const Key('aboutYouError'), tone: BannerTone.danger, text: _error!.message),
              FreshArrowButton(label: 'Continue', loading: _saving, onPressed: _save),
            ],
          ],
        ),
      ),
    );
  }
}
