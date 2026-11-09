// Firebase values for push (D350). They come from build-time --dart-define values, never from a file in the
// repository: FIREBASE_API_KEY, FIREBASE_APP_ID, FIREBASE_MESSAGING_SENDER_ID and FIREBASE_PROJECT_ID (Firebase
// console › Project settings › Your apps › the Android app). The demo and release workflows pass them from the
// repository secrets when they exist (docs/runbooks/mobile-release.md, "Push notifications"). No project, no values:
// push is off and the app says so once in the device log. Part of the values is a mistake: push stays off and the
// log names the missing ones (rule 7: never guess).

import 'push_service.dart';

class FirebaseValues {
  const FirebaseValues({
    required this.apiKey,
    required this.appId,
    required this.messagingSenderId,
    required this.projectId,
  });

  /// The values this build was made with.
  static const fromDefines = FirebaseValues(
    apiKey: String.fromEnvironment('FIREBASE_API_KEY'),
    appId: String.fromEnvironment('FIREBASE_APP_ID'),
    messagingSenderId: String.fromEnvironment('FIREBASE_MESSAGING_SENDER_ID'),
    projectId: String.fromEnvironment('FIREBASE_PROJECT_ID'),
  );

  final String apiKey;
  final String appId;
  final String messagingSenderId;
  final String projectId;

  Map<String, String> get _byName => {
    'FIREBASE_API_KEY': apiKey,
    'FIREBASE_APP_ID': appId,
    'FIREBASE_MESSAGING_SENDER_ID': messagingSenderId,
    'FIREBASE_PROJECT_ID': projectId,
  };

  /// The --dart-define names that are blank.
  List<String> get missing => [
    for (final e in _byName.entries)
      if (e.value.trim().isEmpty) e.key,
  ];

  bool get isEmpty => missing.length == _byName.length;

  bool get isComplete => missing.isEmpty;
}

/// The push service for this build (D350): [firebase] only when every Firebase value is set and the build is not
/// the web preview; otherwise [NoopPushService], with one line in the log saying why push is off.
PushService selectPushService({
  required FirebaseValues values,
  required bool isWeb,
  required PushService Function(FirebaseValues values) firebase,
  required void Function(String message) log,
}) {
  if (isWeb) {
    log('Push notifications are off: the web preview has no push.');
    return NoopPushService();
  }
  if (values.isEmpty) {
    log('Push notifications are off: this build has no Firebase values (docs/runbooks/mobile-release.md).');
    return NoopPushService();
  }
  if (!values.isComplete) {
    log('Push notifications are off: these Firebase values are missing: ${values.missing.join(', ')}.');
    return NoopPushService();
  }
  return firebase(values);
}
