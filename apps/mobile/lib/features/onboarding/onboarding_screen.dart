import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../illustrations/illustration.dart';
import '../../state/core_providers.dart';
import '../../widgets/illustrated.dart';
import '../../widgets/motion.dart';
import '../../widgets/states.dart';

class _Page {
  const _Page(this.slot, this.sticker, this.title, this.body);
  final IllustrationSlot slot;
  final String sticker;
  final String title;
  final String body;
}

const _pages = [
  _Page(
    IllustrationSlot.onboardingFresh,
    'Morning catch',
    'Fresh catch, delivered chilled',
    'Fish, prawns, crabs and shellfish from the morning catch, kept cold from our store to your door.',
  ),
  _Page(
    IllustrationSlot.onboardingCleaned,
    'Your cut',
    'Cleaned the way you cook',
    'Choose whole, steaks, fillets or cleaned. You pay for the net weight after cleaning, never for the waste.',
  ),
  _Page(
    IllustrationSlot.onboardingDelivery,
    'Kept cold',
    'Pick a slot, pay on delivery',
    'Choose a delivery slot that suits you and pay by cash or UPI when your order arrives.',
  ),
];

class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key});

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  final _controller = PageController();
  int _index = 0;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _finish() async {
    await ref.read(appPrefsProvider).setOnboardingDone();
    if (mounted) context.go('/location');
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final last = _index == _pages.length - 1;
    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(onPressed: _finish, child: const Text('Skip')),
            ),
            Expanded(
              child: PageView.builder(
                controller: _controller,
                itemCount: _pages.length,
                onPageChanged: (i) => setState(() => _index = i),
                itemBuilder: (context, i) {
                  final page = _pages[i];
                  return SingleChildScrollView(
                    padding: const EdgeInsets.all(FreshSpacing.s6),
                    child: Column(
                      children: [
                        PolaroidCard(
                          tilt: i.isEven ? -0.012 : 0.012,
                          stickers: [Sticker(label: page.sticker, tone: i == 1 ? StickerTone.teal : StickerTone.coral)],
                          picture: Padding(
                            padding: const EdgeInsets.all(FreshSpacing.s4),
                            child: Illustration(slot: page.slot, size: 220),
                          ),
                        ),
                        const SizedBox(height: FreshSpacing.s8),
                        Semantics(
                          header: true,
                          child: Text(page.title, style: theme.textTheme.headlineMedium, textAlign: TextAlign.center),
                        ),
                        const SizedBox(height: FreshSpacing.s3),
                        Text(
                          page.body,
                          style: theme.textTheme.bodyLarge?.copyWith(color: freshColors(context).textMuted),
                          textAlign: TextAlign.center,
                        ),
                      ],
                    ),
                  );
                },
              ),
            ),
            Semantics(
              label: 'Page ${_index + 1} of ${_pages.length}',
              child: ExcludeSemantics(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    for (var i = 0; i < _pages.length; i++)
                      Container(
                        margin: const EdgeInsets.all(FreshSpacing.s1),
                        width: i == _index ? 24 : 8,
                        height: 8,
                        decoration: BoxDecoration(
                          color: i == _index ? theme.colorScheme.primary : freshColors(context).borderStrong,
                          borderRadius: BorderRadius.circular(FreshRadius.full),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(FreshSpacing.s4),
              child: SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: last
                      ? _finish
                      : () => motionAllowed(context)
                            ? _controller.nextPage(duration: FreshDurations.slow, curve: Curves.easeOutCubic)
                            : _controller.jumpToPage(_index + 1),
                  child: Text(last ? 'Get started' : 'Next'),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
