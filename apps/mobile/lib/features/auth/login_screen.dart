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
import '../../illustrations/illustration.dart';
import '../../state/cart_state.dart';
import '../../state/core_providers.dart';
import '../../state/session.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/commerce.dart';
import '../../widgets/heading.dart';
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
    final c = illustratedColors(context);
    final challenge = _challenge;
    return Scaffold(
      backgroundColor: BlockTone.mango.background(c),
      body: AutofillGroup(
        child: ColorBlockScreen(
          tone: BlockTone.mango,
          eyebrow: 'Sign in',
          heading: challenge == null
              ? const [HeadingLine('Your mobile', script: true), HeadingLine('number', brush: true)]
              : const [HeadingLine('Enter your', script: true), HeadingLine('code', brush: true)],
          slot: IllustrationSlot.loginOtp,
          avatarSize: AvatarSize.small,
          onBack: context.canPop() ? () => context.pop() : null,
          sheetCrossAxis: CrossAxisAlignment.stretch,
          children: [
            SheetNote(
              challenge == null
                  ? 'We will send a one-time code by SMS. Your cart stays with you.'
                  : '${challenge.codeLength} digits sent to ${formatPhone(_e164!)}',
            ),
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
              PrimaryButton(label: 'Send code', loading: _busy, onPressed: _requestOtp),
            ] else ...[
              OtpBoxes(
                fieldKey: const Key('otpField'),
                controller: _code,
                length: challenge.codeLength,
                errorText: _codeError,
                onSubmitted: _verify,
              ),
              PrimaryButton(label: 'Verify', loading: _busy, onPressed: _verify),
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
            Text(
              'By continuing you agree to our terms and privacy policy.',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
            ),
          ],
        ),
      ),
    );
  }
}

/// The one-time code as a row of boxes (the mockup's `.otp`). A real, invisible text field sits on top, so the
/// keyboard, SMS autofill, paste and screen readers all work; the boxes only draw its text.
class OtpBoxes extends StatelessWidget {
  const OtpBoxes({
    super.key,
    required this.controller,
    required this.length,
    required this.onSubmitted,
    this.fieldKey,
    this.errorText,
  });

  final TextEditingController controller;
  final int length;
  final VoidCallback onSubmitted;
  final Key? fieldKey;
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final danger = Theme.of(context).extension<FreshColors>()!.dangerText;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SizedBox(
          height: 52,
          child: Stack(
            children: [
              ListenableBuilder(
                listenable: controller,
                builder: (context, _) {
                  final text = controller.text;
                  return ExcludeSemantics(
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        for (var i = 0; i < length; i++)
                          Container(
                            width: 40,
                            height: 50,
                            margin: const EdgeInsets.symmetric(horizontal: 3.5),
                            alignment: Alignment.center,
                            decoration: BoxDecoration(
                              color: c.sand,
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(
                                color: errorText != null ? danger : (i == text.length ? c.ink : c.line),
                                width: 2,
                              ),
                            ),
                            child: Text(i < text.length ? text[i] : '', style: AppText.price(c.ink, size: 20)),
                          ),
                      ],
                    ),
                  );
                },
              ),
              Positioned.fill(
                child: Opacity(
                  opacity: 0,
                  alwaysIncludeSemantics: true,
                  child: TextField(
                    key: fieldKey,
                    controller: controller,
                    autofocus: true,
                    showCursor: false,
                    keyboardType: TextInputType.number,
                    autofillHints: const [AutofillHints.oneTimeCode],
                    inputFormatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(length)],
                    decoration: const InputDecoration(
                      labelText: 'One-time code',
                      border: InputBorder.none,
                      enabledBorder: InputBorder.none,
                      focusedBorder: InputBorder.none,
                      filled: false,
                    ),
                    onSubmitted: (_) => onSubmitted(),
                  ),
                ),
              ),
            ],
          ),
        ),
        if (errorText != null)
          Padding(
            padding: const EdgeInsets.only(top: 6),
            child: Semantics(
              liveRegion: true,
              child: Text(
                errorText!,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: danger),
              ),
            ),
          ),
      ],
    );
  }
}
