// The shape the server accepts for a search query `q` (products and search/suggest, CAT-R12 in
// docs/product/catalog.md):
// 2 to 60 characters after trimming, with at least one letter or digit. Checked before sending so a short or
// symbol-only search shows a hint instead of a refusal (D235).

/// Shortest search the server accepts, after trimming.
const int searchMinLength = 2;

/// Longest search the server accepts, after trimming.
const int searchMaxLength = 60;

final _letterOrDigit = RegExp(r'[\p{L}\p{N}]', unicode: true);

/// True when [text], trimmed, is a search the server accepts.
bool isSearchable(String text) {
  final q = text.trim();
  return q.length >= searchMinLength && q.length <= searchMaxLength && _letterOrDigit.hasMatch(q);
}
