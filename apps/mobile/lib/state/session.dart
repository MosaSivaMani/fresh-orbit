// Customer session: signed in when a refresh token is held. Login is phone → OTP; the guest cart token is sent
// with verify so the server merges the guest cart (asynchronously, so the cart is re-read after login).

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/models/auth.dart';
import 'core_providers.dart';

class SessionState {
  const SessionState({required this.signedIn, this.user});

  final bool signedIn;
  final AuthUser? user;
}

final sessionProvider = NotifierProvider<SessionController, SessionState>(SessionController.new);

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
    state = SessionState(signedIn: true, user: result.user);
    await _registerPushToken();
    return result;
  }

  Future<void> signOut() async {
    final tokens = ref.read(tokenStoreProvider);
    final refresh = tokens.refreshToken;
    final push = await ref.read(pushServiceProvider).token();
    try {
      if (push != null) await ref.read(accountRepositoryProvider).unregisterDeviceToken(push);
      if (refresh != null) await ref.read(authRepositoryProvider).logout(refresh);
    } catch (_) {
      // Signing out locally must work offline; the server token expires on its own.
    }
    await tokens.clearSession();
    state = const SessionState(signedIn: false);
  }

  /// Called by the auth interceptor when refresh fails or a second 401 arrives.
  void onSessionEnded() {
    if (state.signedIn) state = const SessionState(signedIn: false);
  }

  Future<void> _registerPushToken() async {
    final push = ref.read(pushServiceProvider);
    await push.initialize();
    final token = await push.token();
    if (token == null) return; // NoopPushService until Firebase is set up.
    try {
      await ref.read(accountRepositoryProvider).registerDeviceToken(pushToken: token, platform: devicePlatform());
    } catch (_) {
      // Retried on next sign-in or token refresh; push is not essential for ordering.
    }
  }
}
