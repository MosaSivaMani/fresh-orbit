// Navigation map (docs/design/mobile.md): bottom navigation Home · Shop · Cart · Orders · Account as a
// StatefulShellRoute (each tab keeps its stack); full-screen routes for search, product, login, checkout and
// order success. Settings (theme, language) work signed out. Checkout and account pages that need a customer redirect to login and come back.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import 'core/app_links.dart';
import 'core/format.dart';
import 'features/account/account_screens.dart';
import 'features/account/address_screens.dart';
import 'features/account/help_screen.dart';
import 'features/account/settings_screen.dart';
import 'features/auth/login_screen.dart';
import 'features/auth/profile_details_screen.dart';
import 'features/cart/cart_screen.dart';
import 'features/catalog/home_screen.dart';
import 'features/catalog/product_screen.dart';
import 'features/catalog/search_screen.dart';
import 'features/catalog/shop_screen.dart';
import 'features/catalog/wishlist_screen.dart';
import 'features/checkout/checkout_screen.dart';
import 'features/checkout/order_success_screen.dart';
import 'features/onboarding/location_screen.dart';
import 'features/onboarding/onboarding_screen.dart';
import 'features/onboarding/splash_screen.dart';
import 'features/orders/order_tracking_screen.dart';
import 'features/orders/orders_screen.dart';
import 'state/cart_state.dart';
import 'state/first_run.dart';
import 'state/session.dart';
import 'widgets/bold.dart';
import 'widgets/states.dart';

/// Paths that need a signed-in customer.
bool _needsLogin(String path) =>
    path.startsWith('/checkout') ||
    path.startsWith('/orders/') ||
    path.startsWith('/account/') && !path.startsWith('/account/help') && !path.startsWith('/account/settings') ||
    path.startsWith('/wishlist') ||
    path.startsWith('/addresses') ||
    path == aboutYouPath;

final routerProvider = Provider<GoRouter>((ref) {
  final sessionChanges = ValueNotifier<bool>(ref.read(sessionProvider).signedIn);
  ref.listen(sessionProvider, (_, next) => sessionChanges.value = next.signedIn);
  ref.onDispose(sessionChanges.dispose);
  // The header's back button: pop when a page is below, else home (a screen opened from a link).
  BackCircleButton.defaultBack = (context) {
    if (context.canPop()) {
      context.pop();
    } else {
      context.go('/home');
    }
  };

  final router = GoRouter(
    initialLocation: '/splash',
    refreshListenable: sessionChanges,
    errorBuilder: (context, state) => NotFoundScreen(onHome: () => context.go('/home')),
    redirect: (context, state) {
      // A website link (App Links / Universal Links, D308) uses the website's paths.
      final link = appRouteForWebsitePath(state.uri);
      if (link != null) return link;
      final signedIn = ref.read(sessionProvider).signedIn;
      final path = state.uri.path;
      if (!signedIn && _needsLogin(path)) {
        return Uri(path: '/login', queryParameters: {'from': state.uri.toString()}).toString();
      }
      return null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),
      GoRoute(path: '/onboarding', builder: (_, _) => const OnboardingScreen()),
      GoRoute(
        path: '/location',
        builder: (_, state) => LocationScreen(changing: state.uri.queryParameters['change'] == '1'),
      ),
      GoRoute(
        path: '/search',
        builder: (_, state) => SearchScreen(initialQuery: state.uri.queryParameters['q']),
      ),
      GoRoute(
        path: '/product/:slug',
        builder: (_, state) =>
            ProductScreen(slug: state.pathParameters['slug']!, variantId: state.uri.queryParameters['variant']),
      ),
      GoRoute(path: '/wishlist', builder: (_, _) => const WishlistScreen()),
      GoRoute(
        path: '/login',
        // `welcome=1`: the first-run step after onboarding (D277), with "Skip for now, just browse".
        builder: (_, state) => LoginScreen(
          from: state.uri.queryParameters['from'],
          welcome: state.uri.queryParameters['welcome'] == '1',
        ),
      ),
      // "Tell us about you" (D290): pushed over the first page after a sign-in whose account has no name.
      GoRoute(path: aboutYouPath, builder: (_, _) => const ProfileDetailsScreen()),
      GoRoute(path: '/checkout', builder: (_, _) => const CheckoutScreen()),
      // The address form over checkout (D268): full-screen routes outside the tab shell, so checkout pushes them
      // and gets the saved address back without opening a second shell.
      GoRoute(path: '/addresses/new', builder: (_, _) => const AddressFormScreen()),
      GoRoute(
        path: '/addresses/:addressId',
        builder: (_, state) => AddressFormScreen(addressId: state.pathParameters['addressId']),
      ),
      GoRoute(
        path: '/order-success/:orderId',
        builder: (_, state) => OrderSuccessScreen(orderId: state.pathParameters['orderId']!),
      ),
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => AppShell(shell: shell),
        branches: [
          StatefulShellBranch(
            routes: [GoRoute(path: '/home', builder: (_, _) => const HomeScreen())],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/shop',
                builder: (_, state) => ShopScreen(category: state.uri.queryParameters['category']),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: '/cart', builder: (_, _) => const CartScreen())],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/orders',
                builder: (_, _) => const OrdersScreen(),
                routes: [
                  GoRoute(
                    path: ':orderId',
                    builder: (_, state) => OrderTrackingScreen(orderId: state.pathParameters['orderId']!),
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/account',
                builder: (_, _) => const AccountScreen(),
                routes: [
                  GoRoute(path: 'profile', builder: (_, _) => const ProfileScreen()),
                  GoRoute(
                    path: 'addresses',
                    builder: (_, _) => const AddressesScreen(),
                    routes: [
                      GoRoute(path: 'new', builder: (_, _) => const AddressFormScreen()),
                      GoRoute(
                        path: ':addressId',
                        builder: (_, state) => AddressFormScreen(addressId: state.pathParameters['addressId']),
                      ),
                    ],
                  ),
                  GoRoute(path: 'notifications', builder: (_, _) => const NotificationsScreen()),
                  GoRoute(path: 'preferences', builder: (_, _) => const PreferencesScreen()),
                  GoRoute(path: 'help', builder: (_, _) => const HelpScreen()),
                  GoRoute(path: 'settings', builder: (_, _) => const SettingsScreen()),
                ],
              ),
            ],
          ),
        ],
      ),
    ],
  );
  ref.onDispose(
    askAboutYouAfterSignIn(
      router,
      isDue: () => ref.read(aboutYouDueProvider),
      onAsked: () => ref.read(aboutYouDueProvider.notifier).set(false),
    ),
  );
  return router;
});

/// The five tabs (D88, D176): the floating rounded menu, and on Home and Shop the floating cart bar above it
/// while the cart has lines. Both sit in the bottom bar slot, so they take their own space and never cover a
/// screen's content. The cart bar's amount is the server cart's estimated total, only formatted.
///
/// Android back (D234): a page inside a tab goes back within the tab; the first page of any other tab goes to
/// Home; on Home the first back shows "Press back again to exit" and only a second back within two seconds
/// closes the app.
class AppShell extends ConsumerStatefulWidget {
  const AppShell({super.key, required this.shell});

  final StatefulNavigationShell shell;

  @override
  ConsumerState<AppShell> createState() => _AppShellState();
}

class _AppShellState extends ConsumerState<AppShell> {
  static const exitWindow = Duration(seconds: 2);

  DateTime? _backAt;

  void _onBack(bool didPop, Object? _) {
    if (didPop) return;
    final shell = widget.shell;
    if (shell.currentIndex != 0) {
      shell.goBranch(0);
      return;
    }
    final now = DateTime.now();
    if (_backAt != null && now.difference(_backAt!) < exitWindow) {
      SystemNavigator.pop();
      return;
    }
    _backAt = now;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        const SnackBar(
          key: ValueKey('pressBackToExit'),
          content: Text('Press back again to exit'),
          duration: exitWindow,
        ),
      );
  }

  @override
  Widget build(BuildContext context) {
    final shell = widget.shell;
    final cart = ref.watch(cartProvider).value;
    final cartCount = cart?.lineCount ?? 0;
    final showCartBar = shell.currentIndex <= 1 && cart != null && !cart.isEmpty && cart.totals != null;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: _onBack,
      child: Scaffold(
        body: shell,
        bottomNavigationBar: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const SizedBox(height: 8),
            if (showCartBar)
              FreshCartBar(
                count: cartCount,
                amount: formatRupees(cart.totals!.estimatedTotalPaise),
                // Only lines that have a picture; none leaves the space out (no empty circles).
                thumbnails: [
                  for (final line in cart.lines)
                    if (line.imageUrl case final url? when url.isNotEmpty) url,
                ].take(2).toList(),
                onTap: () => shell.goBranch(2),
              ),
            FoodTabBar(
              currentIndex: shell.currentIndex,
              onSelected: (i) => shell.goBranch(i, initialLocation: i == shell.currentIndex),
              tabs: [
                const FoodTab('Home', Icons.home_outlined),
                const FoodTab('Shop', Icons.grid_view_rounded),
                FoodTab('Cart', Icons.shopping_cart_outlined, badge: cartCount),
                const FoodTab('Orders', Icons.receipt_long_outlined),
                const FoodTab('Account', Icons.person_outline_rounded),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
