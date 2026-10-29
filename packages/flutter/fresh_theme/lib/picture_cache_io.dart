/// The picture disk cache itself (D310), for tests and diagnostics on Android and iOS. Not for the web build; screens
/// use `FreshNetworkImage` from `package:fresh_theme/fresh_theme.dart`.
library;

export 'src/network_picture_io.dart' show DiskCachedNetworkImage, DiskPictureCache, cacheFileName;
