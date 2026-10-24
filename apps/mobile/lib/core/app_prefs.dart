// Non-secret device preferences: chosen pincode, onboarding seen, theme, language. Secrets live in TokenStore.

import 'package:shared_preferences/shared_preferences.dart';

abstract class AppPrefs {
  String? get pincode;
  Future<void> setPincode(String pincode);

  bool get onboardingDone;
  Future<void> setOnboardingDone();

  /// 'system', 'light' or 'dark'.
  String get themeMode;
  Future<void> setThemeMode(String mode);

  /// 'en', 'te' or 'hi'. Only English copy exists so far; the choice is kept for when translations land.
  String get language;
  Future<void> setLanguage(String code);
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

  @override
  String get language => _prefs.getString('fo.language') ?? 'en';

  @override
  Future<void> setLanguage(String code) => _prefs.setString('fo.language', code);
}

class MemoryAppPrefs implements AppPrefs {
  MemoryAppPrefs({this.pincode, this.onboardingDone = false, this.themeMode = 'system', this.language = 'en'});

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

  @override
  String language;

  @override
  Future<void> setLanguage(String code) async => language = code;
}
