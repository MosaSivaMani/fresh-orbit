// Composition root: storage, HTTP client and repositories. Tests override the repository providers with fakes.

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/repositories.dart';
import '../api/token_store.dart';
import '../core/app_prefs.dart';
import '../core/launch.dart';
import '../core/push_service.dart';
import '../core/website.dart';
import 'location_state.dart';
import 'session.dart';

/// Overridden in main.dart with the loaded preferences (and in tests with [MemoryAppPrefs]).
final appPrefsProvider = Provider<AppPrefs>((ref) => MemoryAppPrefs());

/// Overridden in main.dart with a loaded store backed by the platform keystore.
final tokenStoreProvider = Provider<TokenStore>((ref) => TokenStore(MemorySecretStore()));

final apiBaseUrlProvider = Provider<String>((ref) => apiBaseUrlDefine);

/// The store website's address (`--dart-define=WEBSITE_BASE_URL`, D299); empty when not configured.
final websiteBaseUrlProvider = Provider<String>((ref) => websiteBaseUrlDefine);

/// The policy pages on the website; empty when the website is not configured, so every policy link is hidden.
final policyLinksProvider = Provider<Map<PolicyPage, Uri>>((ref) => policyLinks(ref.watch(websiteBaseUrlProvider)));

/// Opens a link outside the app ([openExternal]); tests override it to record the address.
final externalLauncherProvider = Provider<ExternalLauncher>((ref) => openExternal);

final pushServiceProvider = Provider<PushService>((ref) => NoopPushService());

final dioProvider = Provider<Dio>((ref) {
  return createDio(
    baseUrl: ref.watch(apiBaseUrlProvider),
    tokens: ref.watch(tokenStoreProvider),
    pincode: () => ref.read(pincodeProvider),
    onSessionEnded: () => ref.read(sessionProvider.notifier).onSessionEnded(),
  );
});

final apiClientProvider = Provider<ApiClient>((ref) => ApiClient(ref.watch(dioProvider)));

final authRepositoryProvider = Provider<AuthRepository>((ref) => HttpAuthRepository(ref.watch(apiClientProvider)));
final locationRepositoryProvider = Provider<LocationRepository>(
  (ref) => HttpLocationRepository(ref.watch(apiClientProvider)),
);
final catalogRepositoryProvider = Provider<CatalogRepository>(
  (ref) => HttpCatalogRepository(ref.watch(apiClientProvider)),
);
final cartRepositoryProvider = Provider<CartRepository>((ref) => HttpCartRepository(ref.watch(apiClientProvider)));
final orderRepositoryProvider = Provider<OrderRepository>((ref) => HttpOrderRepository(ref.watch(apiClientProvider)));
final accountRepositoryProvider = Provider<AccountRepository>(
  (ref) => HttpAccountRepository(ref.watch(apiClientProvider)),
);

/// `platform` for `PUT /me/device-tokens`.
String devicePlatform() => switch (defaultTargetPlatform) {
  TargetPlatform.iOS => 'IOS',
  _ => 'ANDROID',
};
