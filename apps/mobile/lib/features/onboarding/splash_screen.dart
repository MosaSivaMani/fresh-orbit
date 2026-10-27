import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

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
    final c = illustratedColors(context);
    return Scaffold(
      backgroundColor: BlockTone.teal.background(c),
      body: Center(
        child: Semantics(
          label: 'Fresh Orbitt, loading',
          child: ExcludeSemantics(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // The owner's logo (D119) sits on white, so it gets a white card on the teal block.
                Container(
                  width: 300,
                  padding: const EdgeInsets.all(FreshSpacing.s4),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(FreshRadius.xl),
                  ),
                  child: Image.asset('assets/brand/logo.png', fit: BoxFit.contain),
                ),
                const SizedBox(height: FreshSpacing.s5),
                Text('Fresh Orbitt', style: AppText.blockTitle(BlockTone.teal.foreground(c))),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
