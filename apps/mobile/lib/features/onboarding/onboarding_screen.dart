import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../state/core_providers.dart';
import '../../widgets/states.dart';

class _Page {
  const _Page(this.icon, this.title, this.body);
  final IconData icon;
  final String title;
  final String body;
}

const _pages = [
  _Page(
    Icons.set_meal_outlined,
    'Fresh catch, delivered chilled',
    'Fish, prawns, crabs and shellfish from the morning catch, kept cold from our store to your door.',
  ),
  _Page(
    Icons.content_cut,
    'Cleaned the way you cook',
    'Choose whole, steaks, fillets or cleaned. You pay for the net weight after cleaning, never for the waste.',
  ),
  _Page(
    Icons.payments_outlined,
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
                  return Padding(
                    padding: const EdgeInsets.all(FreshSpacing.s6),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        ExcludeSemantics(child: Icon(page.icon, size: 96, color: freshColors(context).accent)),
                        const SizedBox(height: FreshSpacing.s6),
                        Semantics(
                          header: true,
                          child: Text(page.title, style: theme.textTheme.headlineSmall, textAlign: TextAlign.center),
                        ),
                        const SizedBox(height: FreshSpacing.s3),
                        Text(page.body, style: theme.textTheme.bodyLarge, textAlign: TextAlign.center),
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
                        width: i == _index ? 20 : 8,
                        height: 8,
                        decoration: BoxDecoration(
                          color: i == _index ? theme.colorScheme.primary : freshColors(context).border,
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
                      : () => _controller.nextPage(duration: FreshDurations.normal, curve: Curves.easeOut),
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
