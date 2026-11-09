// Customer session: signed in when a refresh token is held. Login is phone → OTP; the guest cart token is sent
// with verify so the server merges the guest cart (asynchronously, so the cart is re-read after login).

import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/models/auth.dart';
import 'core_providers.dart';
import 'push_state.dart';

class SessionState {
  const SessionState({required this.signedIn, this.user});

  final bool signedIn;
  final AuthUser? user;
}

final sessionProvider = NotifierProvider<SessionController, SessionState>(SessionController.new);

/// "Tell us about you" is due (D290): set by a sign-in whose account has no name yet (the verify answer's
/// `user.fullName` is empty), cleared once the step opens, on sign-out and when the session ends. Kept apart from
/// [SessionState] so the router's sign-in refresh does not fire for it.
final aboutYouDueProvider = NotifierProvider<AboutYouDue, bool>(AboutYouDue.new);

class AboutYouDue extends Notifier<bool> {
  @override
  bool build() => false;

  void set(bool due) => state = due;
}

class SessionController extends Notifier<SessionState> {
  @override
  SessionState build() => SessionState(signedIn: ref.read(tokenStoreProvider).hasSession);

  Future<OtpChallenge> requestOtp(String phone) => ref.read(authRepositoryProvider).requestOtp(phone);

  Future<VerifyOtpResult> verifyOtp({required String challengeId, required String phone, required String code}) async {
    final tokens = ref.read(tokenStoreProvider);
    final result = await ref
        .read(authRepositoryProvider)
        .verifyOtp(
          challengeId: challengeId,
          phone: phone,
          code: code,
          guestCartToken: tokens.guestCartToken,
          deviceLabel: 'Fresh Orbitt app',
        );
    await tokens.saveTokens(result.tokens);
    await tokens.clearGuestCartToken();
    ref.read(aboutYouDueProvider.notifier).set((result.user.fullName ?? '').trim().isEmpty);
    state = SessionState(signedIn: true, user: result.user);
    // Push (D349): this phone's token goes to the new account when notifications are allowed. Not awaited: the
    // sign-in never waits for the push provider.
    unawaited(ref.read(pushCoordinatorProvider).registerIfPermitted());
    return result;
  }

  Future<void> signOut() async {
    final tokens = ref.read(tokenStoreProvider);
    final refresh = tokens.refreshToken;
    // Push (D349): this phone stops receiving the account's notifications. Before the logout, while the access
    // token still works.
    await ref.read(pushCoordinatorProvider).unregister();
    try {
      if (refresh != null) await ref.read(authRepositoryProvider).logout(refresh);
    } catch (_) {
      // Signing out locally must work offline; the server token expires on its own.
    }
    await tokens.clearSession();
    ref.read(aboutYouDueProvider.notifier).set(false);
    state = const SessionState(signedIn: false);
  }

  /// Called by the auth interceptor when refresh fails or a second 401 arrives.
  void onSessionEnded() {
    ref.read(aboutYouDueProvider.notifier).set(false);
    if (state.signedIn) state = const SessionState(signedIn: false);
  }
}
