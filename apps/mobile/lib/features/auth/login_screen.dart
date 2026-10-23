// Login: phone → OTP (docs/api §4.1). The guest cart token goes with verify; the server merges the guest cart
// asynchronously, so the cart is re-read right after login and once more shortly after.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/auth.dart';
import '../../core/format.dart';
import '../../state/cart_state.dart';
import '../../state/core_providers.dart';
import '../../state/session.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key, this.from});

  /// Where to go after login (e.g. /checkout).
  final String? from;

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _phone = TextEditingController();
  final _code = TextEditingController();
  String? _e164;
  OtpChallenge? _challenge;
  String? _phoneError;
  String? _codeError;
  bool _busy = false;
  int _resendIn = 0;
  Timer? _timer;

  @override
  void dispose() {
    _timer?.cancel();
    _phone.dispose();
    _code.dispose();
    super.dispose();
  }

  void _startResendTimer(int seconds) {
    _timer?.cancel();
    setState(() => _resendIn = seconds);
    _timer = Timer.periodic(const Duration(seconds: 1), (t) {
      if (!mounted || _resendIn <= 1) {
        t.cancel();
        if (mounted) setState(() => _resendIn = 0);
        return;
      }
      setState(() => _resendIn--);
    });
  }

  Future<void> _requestOtp() async {
    final e164 = toIndianE164(_phone.text);
    if (e164 == null) {
      setState(() => _phoneError = 'Enter a 10-digit mobile number.');
      return;
    }
    setState(() {
      _busy = true;
      _phoneError = null;
      _codeError = null;
    });
    try {
      final challenge = await ref.read(sessionProvider.notifier).requestOtp(e164);
      setState(() {
        _e164 = e164;
        _challenge = challenge;
        _code.clear();
      });
      _startResendTimer(challenge.resendAfterSeconds);
    } on ApiError catch (e) {
      setState(() => _phoneError = e.fieldMessage('phone') ?? _waitMessage(e) ?? e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String? _waitMessage(ApiError e) =>
      e.retryAfterSeconds == null ? null : 'Too many attempts. Try again in ${e.retryAfterSeconds} seconds.';

  Future<void> _verify() async {
    final challenge = _challenge!;
    final code = _code.text.trim();
    if (code.length != challenge.codeLength) {
      setState(() => _codeError = 'Enter the ${challenge.codeLength}-digit code.');
      return;
    }
    setState(() {
      _busy = true;
      _codeError = null;
    });
    final hadGuestCart = ref.read(tokenStoreProvider).guestCartToken != null;
    try {
      await ref.read(sessionProvider.notifier).verifyOtp(challengeId: challenge.challengeId, phone: _e164!, code: code);
      if (hadGuestCart) {
        // Merge runs on the server after login; read the cart again once it has had time to finish.
        unawaited(Future<void>.delayed(const Duration(seconds: 2), () => ref.invalidate(cartProvider)));
      }
      if (!mounted) return;
      final target = widget.from;
      if (target != null && target.startsWith('/')) {
        context.go(target);
      } else if (context.canPop()) {
        context.pop();
      } else {
        context.go('/home');
      }
    } on ApiError catch (e) {
      setState(() {
        switch (e.code) {
          case 'OTP_TOO_MANY_ATTEMPTS':
            _challenge = null;
            _phoneError = 'Too many wrong codes. Request a new code.';
          case 'OTP_EXPIRED':
            _codeError = 'This code has expired. Tap "Resend code".';
          default:
            _codeError = e.fieldMessage('code') ?? e.message;
        }
      });
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final challenge = _challenge;
    return Scaffold(
      appBar: AppBar(title: const Text('Sign in')),
      body: SafeArea(
        child: AutofillGroup(
          child: ListView(
            padding: const EdgeInsets.all(FreshSpacing.s4),
            children: [
              Semantics(
                header: true,
                child: Text(
                  challenge == null ? 'Sign in with your mobile number' : 'Enter the code',
                  style: theme.textTheme.headlineSmall,
                ),
              ),
              const SizedBox(height: FreshSpacing.s2),
              Text(
                challenge == null
                    ? 'We will send a one-time code by SMS. Your cart stays with you.'
                    : 'We sent a ${challenge.codeLength}-digit code to ${formatPhone(_e164!)}.',
                style: theme.textTheme.bodyMedium,
              ),
              const SizedBox(height: FreshSpacing.s5),
              if (challenge == null) ...[
                TextField(
                  key: const Key('phoneField'),
                  controller: _phone,
                  keyboardType: TextInputType.phone,
                  autofillHints: const [AutofillHints.telephoneNumberNational],
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(10)],
                  decoration: InputDecoration(labelText: 'Mobile number', prefixText: '+91 ', errorText: _phoneError),
                  onSubmitted: (_) => _requestOtp(),
                ),
                const SizedBox(height: FreshSpacing.s4),
                PrimaryButton(label: 'Send code', loading: _busy, onPressed: _requestOtp),
              ] else ...[
                TextField(
                  key: const Key('otpField'),
                  controller: _code,
                  autofocus: true,
                  keyboardType: TextInputType.number,
                  autofillHints: const [AutofillHints.oneTimeCode],
                  inputFormatters: [
                    FilteringTextInputFormatter.digitsOnly,
                    LengthLimitingTextInputFormatter(challenge.codeLength),
                  ],
                  decoration: InputDecoration(labelText: 'One-time code', errorText: _codeError),
                  onSubmitted: (_) => _verify(),
                ),
                const SizedBox(height: FreshSpacing.s4),
                PrimaryButton(label: 'Verify and continue', loading: _busy, onPressed: _verify),
                const SizedBox(height: FreshSpacing.s2),
                Row(
                  children: [
                    TextButton(
                      onPressed: _busy ? null : () => setState(() => _challenge = null),
                      child: const Text('Change number'),
                    ),
                    const Spacer(),
                    TextButton(
                      onPressed: _busy || _resendIn > 0 ? null : _requestOtp,
                      child: Text(_resendIn > 0 ? 'Resend in $_resendIn s' : 'Resend code'),
                    ),
                  ],
                ),
              ],
              const SizedBox(height: FreshSpacing.s5),
              Text(
                'By continuing you agree to our terms and privacy policy.',
                style: theme.textTheme.bodySmall?.copyWith(color: freshColors(context).textMuted),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
