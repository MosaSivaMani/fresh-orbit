// Picks one photo from the device gallery (image_picker, BSD-3). Behind a provider so widget tests replace it.

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../state/orders_state.dart';

/// Opens the gallery; null when the customer closes it without a choice. The photo is resized on the device
/// (longest side 2048 px) so uploads stay small; the server checks type and size again.
typedef PhotoPicker = Future<ComplaintPhotoUpload?> Function();

final photoPickerProvider = Provider<PhotoPicker>((ref) => _pickFromGallery);

Future<ComplaintPhotoUpload?> _pickFromGallery() async {
  final file = await ImagePicker().pickImage(source: ImageSource.gallery, maxWidth: 2048, maxHeight: 2048, imageQuality: 85);
  if (file == null) return null;
  return ComplaintPhotoUpload(bytes: await file.readAsBytes(), fileName: file.name);
}
