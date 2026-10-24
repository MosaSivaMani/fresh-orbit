// Navigation map (docs/design/mobile.md): bottom navigation Home · Shop · Cart · Orders · Account as a
// StatefulShellRoute (each tab keeps its stack); full-screen routes for search, product, login, checkout and
// order success. Settings (theme, language) work signed out. Checkout and account pages that need a customer redirect to login and come back.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'core/format.dart';
import 'features/account/account_screens.dart';
import 'features/account/address_screens.dart';
import 'features/account/help_screen.dart';
import 'features/account/settings_screen.dart';
import 'features/auth/login_screen.dart';
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
import 'state/session.dart';
import 'widgets/bold.dart';
import 'widgets/states.dart';

/// Paths that need a signed-in customer.
bool _needsLogin(String path) =>
    path.startsWith('/checkout') ||
    path.startsWith('/orders/') ||
    path.startsWith('/account/') && !path.startsWith('/account/help') && !path.startsWith('/account/settings') ||
    path.startsWith('/wishlist');

final routerProvider = Provider<GoRouter>((ref) {
  final sessionChanges = ValueNotifier<bool>(ref.read(sessionProvider).signedIn);
  ref.listen(sessionProvider, (_, next) => sessionChanges.value = next.signedIn);
  ref.onDispose(sessionChanges.dispose);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: sessionChanges,
    errorBuilder: (context, state) => NotFoundScreen(onHome: () => context.go('/home')),
    redirect: (context, state) {
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
        builder: (_, state) => ProductScreen(slug: state.pathParameters['slug']!),
      ),
      GoRoute(path: '/wishlist', builder: (_, _) => const WishlistScreen()),
      GoRoute(
        path: '/login',
        builder: (_, state) => LoginScreen(from: state.uri.queryParameters['from']),
      ),
      GoRoute(path: '/checkout', builder: (_, _) => const CheckoutScreen()),
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
});

/// The five tabs (D88, D176): the floating rounded menu, and on Home and Shop the floating cart bar above it
/// while the cart has lines. Both sit in the bottom bar slot, so they take their own space and never cover a
/// screen's content. The cart bar's amount is the server cart's subtotal, only formatted.
class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.shell});

  final StatefulNavigationShell shell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cart = ref.watch(cartProvider).value;
    final cartCount = cart?.lineCount ?? 0;
    final showCartBar = shell.currentIndex <= 1 && cart != null && !cart.isEmpty && cart.totals != null;
    return Scaffold(
      body: shell,
      bottomNavigationBar: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(height: 8),
          if (showCartBar)
            ViewCartBar(
              count: cartCount,
              amount: formatRupees(cart.totals!.subtotalPaise),
              thumbnails: [for (final line in cart.lines.take(2)) line.imageUrl],
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
    );
  }
}
