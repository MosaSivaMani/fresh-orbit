import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

/// Opens a phone number, e-mail or web link outside the app; shows a message when nothing can open it.
Future<void> openExternal(BuildContext context, Uri uri) async {
  var opened = false;
  try {
    opened = await launchUrl(uri, mode: LaunchMode.externalApplication);
  } catch (_) {
    opened = false;
  }
  if (!opened && context.mounted) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Could not open ${uri.toString()}.')));
  }
}

Uri telUri(String e164) => Uri(scheme: 'tel', path: e164);
