// Onboarding (D88): three colour-block pages (coral, teal, mango), each with its character in the ringed circle,
// a sticker on the circle's edge and a paper sheet with the story, page dots and the next button.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../illustrations/illustration.dart';
import '../../state/core_providers.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/heading.dart';
import '../../widgets/illustrated.dart';
import '../../widgets/motion.dart';

class _Page {
  const _Page({
    required this.tone,
    required this.slot,
    required this.big,
    required this.sticker,
    required this.stickerTone,
    required this.stickerOnLeft,
    required this.title,
    required this.body,
  });

  final BlockTone tone;
  final IllustrationSlot slot;
  final List<HeadingLine> big;
  final String sticker;
  final StickerTone stickerTone;
  final bool stickerOnLeft;
  final String title;
  final String body;
}

const _pages = [
  _Page(
    tone: BlockTone.coral,
    slot: IllustrationSlot.onboardingFresh,
    big: [HeadingLine('Straight off', script: true), HeadingLine('the boats', brush: true)],
    sticker: 'Morning catch',
    stickerTone: StickerTone.mango,
    stickerOnLeft: true,
    title: 'Hours old, not days',
    body: 'Fish, prawns and crabs from the morning catch, kept cold from our store to your door.',
  ),
  _Page(
    tone: BlockTone.teal,
    slot: IllustrationSlot.onboardingCleaned,
    big: [HeadingLine('Cleaned'), HeadingLine('your way', script: true)],
    sticker: 'Your cut',
    stickerTone: StickerTone.coral,
    stickerOnLeft: false,
    title: 'Steaks, curry cut, fillet',
    body: 'Pick the cut. We weigh it after cleaning, so you pay for what you cook, never for the waste.',
  ),
  _Page(
    tone: BlockTone.mango,
    slot: IllustrationSlot.onboardingDelivery,
    big: [HeadingLine('At your'), HeadingLine('door', script: true, brush: true)],
    sticker: 'Kept cold',
    stickerTone: StickerTone.teal,
    stickerOnLeft: true,
    title: 'Pick a delivery slot',
    body: 'Your order comes in a cold box. Pay by cash or UPI when it arrives.',
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

  Future<void> _next() async {
    if (_index == _pages.length - 1) {
      await _finish();
    } else if (motionAllowed(context)) {
      await _controller.nextPage(duration: FreshDurations.slow, curve: Curves.easeOutCubic);
    } else {
      _controller.jumpToPage(_index + 1);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Scaffold(
      backgroundColor: _pages[_index].tone.background(c),
      body: PageView.builder(
        controller: _controller,
        itemCount: _pages.length,
        onPageChanged: (i) => setState(() => _index = i),
        itemBuilder: (context, i) {
          final page = _pages[i];
          final last = i == _pages.length - 1;
          final fg = page.tone.foreground(c);
          return ColorBlockScreen(
            tone: page.tone,
            eyebrow: 'Step ${i + 1} of ${_pages.length}',
            heading: page.big,
            slot: page.slot,
            trailing: TextButton(
              style: TextButton.styleFrom(foregroundColor: fg),
              onPressed: _finish,
              child: const Text('Skip'),
            ),
            sticker: StickerSpot(
              left: page.stickerOnLeft ? 28 : null,
              right: page.stickerOnLeft ? null : 26,
              top: 44,
              child: Sticker(label: page.sticker, tone: page.stickerTone, tilt: page.stickerOnLeft ? -0.022 : 0.02),
            ),
            children: [
              SheetTitle(page.title),
              SheetNote(page.body),
              PageDots(count: _pages.length, index: i),
              SizedBox(
                width: double.infinity,
                child: FilledButton(onPressed: _next, child: Text(last ? 'Get started →' : 'Next →')),
              ),
            ],
          );
        },
      ),
    );
  }
}
