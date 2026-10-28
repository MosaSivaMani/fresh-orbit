// Text fields in the sign-in family (D290): the same rounded paper outline as [FreshPhoneField], with a label
// above the box and the server's sentence under it. The field lays out what it is given; it checks nothing.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// A labelled text field on the cream card: the [label] above (a screen reader hears it as the field's label),
/// a rounded paper box with a thin line (danger coloured with an [errorText]) and the [errorText] under it as a
/// live region. Pass [fieldKey] to find the inner [TextField] in tests.
class FreshTextField extends StatelessWidget {
  const FreshTextField({
    super.key,
    required this.controller,
    required this.label,
    this.fieldKey,
    this.hint,
    this.errorText,
    this.keyboardType,
    this.textInputAction,
    this.textCapitalization = TextCapitalization.none,
    this.autofillHints,
    this.inputFormatters,
    this.onSubmitted,
    this.enabled = true,
  });

  final TextEditingController controller;
  final String label;
  final Key? fieldKey;
  final String? hint;
  final String? errorText;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final TextCapitalization textCapitalization;
  final Iterable<String>? autofillHints;
  final List<TextInputFormatter>? inputFormatters;
  final ValueChanged<String>? onSubmitted;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final danger = freshColors(context).dangerText;
    final hasError = errorText != null;
    final theme = Theme.of(context);
    final textStyle = theme.textTheme.titleMedium?.copyWith(color: c.ink, fontSize: 17);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Padding(
          padding: const EdgeInsets.only(left: 4, bottom: 6),
          child: ExcludeSemantics(
            child: Text(label, style: theme.textTheme.labelLarge?.copyWith(color: c.ink, fontWeight: FontWeight.w700)),
          ),
        ),
        Container(
          constraints: const BoxConstraints(minHeight: 56),
          padding: const EdgeInsets.symmetric(horizontal: 16),
          decoration: BoxDecoration(
            color: c.paper,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: hasError ? danger : c.line, width: 1.5),
          ),
          alignment: Alignment.centerLeft,
          child: Semantics(
            label: label,
            child: TextField(
              key: fieldKey,
              controller: controller,
              enabled: enabled,
              keyboardType: keyboardType,
              textInputAction: textInputAction,
              textCapitalization: textCapitalization,
              autofillHints: autofillHints,
              inputFormatters: inputFormatters,
              style: textStyle,
              decoration: InputDecoration(
                hintText: hint,
                hintStyle: textStyle?.copyWith(color: c.muted, fontWeight: FontWeight.w400),
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                disabledBorder: InputBorder.none,
                errorBorder: InputBorder.none,
                focusedErrorBorder: InputBorder.none,
                filled: false,
                isDense: true,
                contentPadding: const EdgeInsets.symmetric(vertical: 16),
              ),
              onSubmitted: onSubmitted,
            ),
          ),
        ),
        if (hasError)
          Padding(
            padding: const EdgeInsets.only(top: 6, left: 4),
            child: Semantics(
              liveRegion: true,
              child: Text(errorText!, style: theme.textTheme.bodySmall?.copyWith(color: danger)),
            ),
          ),
      ],
    );
  }
}
