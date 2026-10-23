// Non-secret device preferences: chosen pincode, onboarding seen, theme. Secrets live in TokenStore.

import 'package:shared_preferences/shared_preferences.dart';

abstract class AppPrefs {
  String? get pincode;
  Future<void> setPincode(String pincode);

  bool get onboardingDone;
  Future<void> setOnboardingDone();

  /// 'system', 'light' or 'dark'.
  String get themeMode;
  Future<void> setThemeMode(String mode);
}

class SharedAppPrefs implements AppPrefs {
  SharedAppPrefs(this._prefs);

  static Future<SharedAppPrefs> load() async => SharedAppPrefs(await SharedPreferences.getInstance());

  final SharedPreferences _prefs;

  @override
  String? get pincode => _prefs.getString('fo.pincode');

  @override
  Future<void> setPincode(String pincode) => _prefs.setString('fo.pincode', pincode);

  @override
  bool get onboardingDone => _prefs.getBool('fo.onboardingDone') ?? false;

  @override
  Future<void> setOnboardingDone() => _prefs.setBool('fo.onboardingDone', true);

  @override
  String get themeMode => _prefs.getString('fo.themeMode') ?? 'system';

  @override
  Future<void> setThemeMode(String mode) => _prefs.setString('fo.themeMode', mode);
}

class MemoryAppPrefs implements AppPrefs {
  MemoryAppPrefs({this.pincode, this.onboardingDone = false, this.themeMode = 'system'});

  @override
  String? pincode;

  @override
  bool onboardingDone;

  @override
  String themeMode;

  @override
  Future<void> setPincode(String value) async => pincode = value;

  @override
  Future<void> setOnboardingDone() async => onboardingDone = true;

  @override
  Future<void> setThemeMode(String mode) async => themeMode = mode;
}
