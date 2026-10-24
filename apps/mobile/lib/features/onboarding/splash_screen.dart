import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../illustrations/illustration.dart';
import '../../state/core_providers.dart';
import '../../state/location_state.dart';

/// First screen: decides where to go (onboarding → pincode → home). Tokens and preferences are already
/// loaded in main(); the session is refreshed lazily by the first authenticated request.
class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _route());
  }

  void _route() {
    if (!mounted) return;
    final prefs = ref.read(appPrefsProvider);
    final target = !prefs.onboardingDone
        ? '/onboarding'
        : ref.read(pincodeProvider) == null
        ? '/location'
        : '/home';
    context.go(target);
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      backgroundColor: scheme.primary,
      body: Center(
        child: Semantics(
          label: 'Fresh Orbit, loading',
          child: ExcludeSemantics(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Illustration(slot: IllustrationSlot.homeHero, size: 160, excludeFromSemantics: true),
                const SizedBox(height: FreshSpacing.s4),
                Text(
                  'Fresh Orbit',
                  style: Theme.of(context).textTheme.headlineMedium?.copyWith(color: scheme.onPrimary),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
