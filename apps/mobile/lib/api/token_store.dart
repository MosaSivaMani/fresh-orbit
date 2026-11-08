// Token and guest-cart storage. The refresh token and the guest cart token live in the platform keystore
// (flutter_secure_storage: Android Keystore-backed encrypted prefs, iOS Keychain). The access token is kept in
// memory only (15 min lifetime); after a restart the first request refreshes it.

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'models/auth.dart';

/// Minimal key/value secret store, so tests can use memory instead of the platform keystore.
abstract class SecretStore {
  Future<String?> read(String key);
  Future<void> write(String key, String value);
  Future<void> delete(String key);
}

class SecureSecretStore implements SecretStore {
  SecureSecretStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  @override
  Future<String?> read(String key) => _storage.read(key: key);

  @override
  Future<void> write(String key, String value) => _storage.write(key: key, value: value);

  @override
  Future<void> delete(String key) => _storage.delete(key: key);
}

class MemorySecretStore implements SecretStore {
  MemorySecretStore([Map<String, String>? initial]) : values = {...?initial};

  final Map<String, String> values;

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async => values[key] = value;

  @override
  Future<void> delete(String key) async => values.remove(key);
}

class TokenStore {
  TokenStore(this._secrets);

  static const _refreshKey = 'fo.refreshToken';
  static const _cartKey = 'fo.guestCartToken';

  final SecretStore _secrets;

  String? _accessToken;
  DateTime? _accessExpiresAt;
  String? _refreshToken;
  String? _guestCartToken;
  bool _loaded = false;

  String? get accessToken => _accessToken;
  DateTime? get accessExpiresAt => _accessExpiresAt;
  String? get refreshToken => _refreshToken;
  String? get guestCartToken => _guestCartToken;

  /// Signed in = we hold a refresh token (the access token may need a refresh).
  bool get hasSession => _refreshToken != null;

  /// Access token missing or expiring within [skew].
  bool accessExpired(DateTime now, {Duration skew = const Duration(seconds: 30)}) =>
      _accessToken == null || _accessExpiresAt == null || !now.toUtc().add(skew).isBefore(_accessExpiresAt!);

  /// Reads the stored tokens. A keystore that cannot be read (for example files restored onto a new phone whose
  /// key does not match, ME-6) never blocks the launch: the unreadable secrets are wiped and the app starts
  /// signed out with a new guest cart.
  Future<void> load() async {
    if (_loaded) return;
    try {
      _refreshToken = await _secrets.read(_refreshKey);
      _guestCartToken = await _secrets.read(_cartKey);
    } catch (_) {
      _refreshToken = null;
      _guestCartToken = null;
      loadFailed = true;
      for (final key in [_refreshKey, _cartKey]) {
        try {
          await _secrets.delete(key);
        } catch (_) {
          // Nothing more to do: the next save overwrites it.
        }
      }
    }
    _loaded = true;
  }

  /// True when [load] could not read the keystore and started signed out.
  bool loadFailed = false;

  Future<void> saveTokens(TokenPair pair) async {
    _accessToken = pair.accessToken;
    _accessExpiresAt = pair.accessTokenExpiresAt;
    _refreshToken = pair.refreshToken;
    await _secrets.write(_refreshKey, pair.refreshToken);
  }

  Future<void> clearSession() async {
    _accessToken = null;
    _accessExpiresAt = null;
    _refreshToken = null;
    await _secrets.delete(_refreshKey);
  }

  Future<void> saveGuestCartToken(String token) async {
    if (token == _guestCartToken) return;
    _guestCartToken = token;
    await _secrets.write(_cartKey, token);
  }

  Future<void> clearGuestCartToken() async {
    _guestCartToken = null;
    await _secrets.delete(_cartKey);
  }
}
