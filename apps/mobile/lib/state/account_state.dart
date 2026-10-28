// Account: profile (If-Match), addresses, wishlist, in-app notifications and preferences.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/models/account.dart';
import 'core_providers.dart';
import 'location_state.dart';
import 'session.dart';

final profileProvider = AsyncNotifierProvider<ProfileController, Profile?>(ProfileController.new);

class ProfileController extends AsyncNotifier<Profile?> {
  @override
  Future<Profile?> build() async {
    if (!ref.watch(sessionProvider.select((s) => s.signedIn))) return null;
    return ref.read(accountRepositoryProvider).profile();
  }

  Future<void> save({String? fullName, String? email, bool? marketingOptIn}) async {
    final current = state.value;
    if (current == null) return;
    final updated = await ref
        .read(accountRepositoryProvider)
        .updateProfile(fullName: fullName, email: email, marketingOptIn: marketingOptIn, version: current.version);
    state = AsyncData(updated);
  }

  /// DELETE /me with If-Match. Refused with active orders (409 INVALID_STATUS_TRANSITION) → rethrown.
  Future<void> deleteAccount() async {
    final current = state.value;
    if (current == null) return;
    await ref.read(accountRepositoryProvider).deleteAccount(version: current.version);
    await ref.read(sessionProvider.notifier).signOut();
  }
}

final addressesProvider = AsyncNotifierProvider<AddressesController, List<Address>>(AddressesController.new);

class AddressesController extends AsyncNotifier<List<Address>> {
  @override
  Future<List<Address>> build() async {
    if (!ref.watch(sessionProvider.select((s) => s.signedIn))) return const [];
    return ref.read(accountRepositoryProvider).addresses();
  }

  Future<Address> add(AddressInput input) async {
    final created = await ref.read(accountRepositoryProvider).addAddress(input);
    await _reload();
    return created;
  }

  Future<Address> replace(Address existing, AddressInput input) async {
    final saved = await ref
        .read(accountRepositoryProvider)
        .replaceAddress(existing.id, input, version: existing.version);
    await _reload();
    return saved;
  }

  Future<void> delete(String addressId) async {
    await ref.read(accountRepositoryProvider).deleteAddress(addressId);
    await _reload();
  }

  Future<void> makeDefault(String addressId) async {
    await ref.read(accountRepositoryProvider).makeDefault(addressId);
    await _reload();
  }

  // The default flag moves between rows on the server (CUS-R4): re-read the list instead of patching it.
  Future<void> _reload() async {
    state = AsyncData(await ref.read(accountRepositoryProvider).addresses());
  }
}

final wishlistProvider = AsyncNotifierProvider<WishlistController, List<WishlistEntry>>(WishlistController.new);

/// Product ids on the wishlist (heart icons).
final wishlistIdsProvider = Provider<Set<String>>(
  (ref) => {...?ref.watch(wishlistProvider).value?.map((e) => e.productId)},
);

class WishlistController extends AsyncNotifier<List<WishlistEntry>> {
  @override
  Future<List<WishlistEntry>> build() async {
    if (!ref.watch(sessionProvider.select((s) => s.signedIn))) return const [];
    ref.watch(pincodeProvider);
    return ref.read(accountRepositoryProvider).wishlist();
  }

  Future<void> toggle(String productId) async {
    final repo = ref.read(accountRepositoryProvider);
    final ids = ref.read(wishlistIdsProvider);
    if (ids.contains(productId)) {
      await repo.removeFromWishlist(productId);
    } else {
      await repo.addToWishlist(productId);
    }
    state = AsyncData(await repo.wishlist());
  }
}

final inboxProvider = AsyncNotifierProvider<InboxController, InboxPage>(InboxController.new);

class InboxController extends AsyncNotifier<InboxPage> {
  // Read again when the customer signs in or out, so one customer never sees another's inbox (D268).
  @override
  Future<InboxPage> build() async {
    if (!ref.watch(sessionProvider.select((s) => s.signedIn))) return InboxPage.empty;
    return ref.read(accountRepositoryProvider).notifications();
  }

  Future<void> markRead(String messageId) async {
    await ref.read(accountRepositoryProvider).markRead(messageId);
    state = AsyncData(await ref.read(accountRepositoryProvider).notifications());
  }

  Future<void> markAllRead() async {
    await ref.read(accountRepositoryProvider).markAllRead();
    state = AsyncData(await ref.read(accountRepositoryProvider).notifications());
  }
}

final preferencesProvider = AsyncNotifierProvider<PreferencesController, List<NotificationPreference>>(
  PreferencesController.new,
);

class PreferencesController extends AsyncNotifier<List<NotificationPreference>> {
  @override
  Future<List<NotificationPreference>> build() async {
    if (!ref.watch(sessionProvider.select((s) => s.signedIn))) return const [];
    return ref.read(accountRepositoryProvider).preferences();
  }

  /// PUT replaces the full set; the server refuses disabling ORDER_UPDATES SMS (PREFERENCE_REQUIRED).
  Future<void> setEnabled(NotificationPreference preference, bool enabled) async {
    final current = state.value ?? const [];
    final next = [
      for (final p in current)
        if (p.category == preference.category && p.channel == preference.channel) p.copyWith(enabled: enabled) else p,
    ];
    state = AsyncData(await ref.read(accountRepositoryProvider).replacePreferences(next));
  }
}
