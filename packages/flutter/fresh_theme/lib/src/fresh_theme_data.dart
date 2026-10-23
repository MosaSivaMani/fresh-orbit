part of '../fresh_theme.dart';

/// Builds Material 3 [ThemeData] from the generated Fresh Orbit tokens.
///
/// ```dart
/// MaterialApp(theme: FreshTheme.light(), darkTheme: FreshTheme.dark());
/// final colors = Theme.of(context).extension<FreshColors>()!; // success, warning, highlight...
/// ```
abstract final class FreshTheme {
  static ThemeData light() => _build(FreshColors.light, Brightness.light);

  static ThemeData dark() => _build(FreshColors.dark, Brightness.dark);

  static ThemeData _build(FreshColors c, Brightness brightness) {
    final scheme = ColorScheme(
      brightness: brightness,
      primary: c.primary,
      onPrimary: c.onPrimary,
      primaryContainer: c.accentSubtle,
      onPrimaryContainer: c.accentText,
      secondary: c.accent,
      onSecondary: c.onAccent,
      secondaryContainer: c.accentSubtle,
      onSecondaryContainer: c.accentText,
      tertiary: c.highlight,
      onTertiary: c.onHighlight,
      tertiaryContainer: c.highlightSubtle,
      onTertiaryContainer: c.highlightText,
      error: c.danger,
      onError: c.onDanger,
      errorContainer: c.dangerSubtle,
      onErrorContainer: c.dangerText,
      surface: c.surface,
      onSurface: c.text,
      onSurfaceVariant: c.textMuted,
      surfaceContainerLowest: c.surface,
      surfaceContainerLow: c.surface,
      surfaceContainer: c.surfaceRaised,
      surfaceContainerHigh: c.muted,
      surfaceContainerHighest: c.muted,
      outline: c.borderStrong,
      outlineVariant: c.border,
      shadow: Colors.black,
      scrim: c.overlay,
      inverseSurface: c.text,
      onInverseSurface: c.textInverse,
      inversePrimary: c.accent,
    );

    final textTheme = _textTheme(c.text);
    final radiusMd = BorderRadius.circular(FreshRadius.md);

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: c.background,
      canvasColor: c.background,
      fontFamily: FreshFonts.sans,
      fontFamilyFallback: FreshFonts.sansFallback,
      textTheme: textTheme,
      dividerTheme: DividerThemeData(color: c.border, thickness: 1, space: 1),
      cardTheme: CardThemeData(
        color: c.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FreshRadius.lg),
          side: BorderSide(color: c.border),
        ),
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: c.surface,
        foregroundColor: c.text,
        elevation: 0,
        scrolledUnderElevation: 1,
        titleTextStyle: textTheme.titleLarge,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: c.primary,
          foregroundColor: c.onPrimary,
          minimumSize: const Size(48, 48),
          padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s5, vertical: FreshSpacing.s3),
          shape: RoundedRectangleBorder(borderRadius: radiusMd),
          textStyle: textTheme.labelLarge,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: c.text,
          minimumSize: const Size(48, 48),
          side: BorderSide(color: c.borderStrong),
          shape: RoundedRectangleBorder(borderRadius: radiusMd),
          textStyle: textTheme.labelLarge,
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: c.accentText,
          minimumSize: const Size(48, 48),
          textStyle: textTheme.labelLarge,
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: c.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s3, vertical: FreshSpacing.s3),
        border: OutlineInputBorder(borderRadius: radiusMd, borderSide: BorderSide(color: c.borderStrong)),
        enabledBorder: OutlineInputBorder(borderRadius: radiusMd, borderSide: BorderSide(color: c.borderStrong)),
        focusedBorder: OutlineInputBorder(borderRadius: radiusMd, borderSide: BorderSide(color: c.ring, width: 2)),
        errorBorder: OutlineInputBorder(borderRadius: radiusMd, borderSide: BorderSide(color: c.danger)),
        focusedErrorBorder: OutlineInputBorder(borderRadius: radiusMd, borderSide: BorderSide(color: c.danger, width: 2)),
        labelStyle: TextStyle(color: c.textMuted),
        hintStyle: TextStyle(color: c.textMuted),
        errorStyle: TextStyle(color: c.dangerText),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: c.muted,
        labelStyle: textTheme.labelMedium,
        side: BorderSide(color: c.border),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(FreshRadius.full)),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: c.text,
        contentTextStyle: TextStyle(color: c.textInverse),
        behavior: SnackBarBehavior.floating,
      ),
      extensions: <ThemeExtension<dynamic>>[c],
    );
  }

  static TextStyle _style(FreshTypeSize s, FontWeight weight, Color color, {String? family}) => TextStyle(
        fontFamily: family ?? FreshFonts.sans,
        fontFamilyFallback: FreshFonts.sansFallback,
        fontSize: s.size,
        height: s.height,
        fontWeight: weight,
        color: color,
      );

  static TextTheme _textTheme(Color color) {
    const display = FreshFonts.display;
    return TextTheme(
      displayLarge: _style(FreshTypeScale.xl5, FreshFontWeights.bold, color, family: display),
      displayMedium: _style(FreshTypeScale.xl4, FreshFontWeights.bold, color, family: display),
      displaySmall: _style(FreshTypeScale.xl3, FreshFontWeights.bold, color, family: display),
      headlineLarge: _style(FreshTypeScale.xl3, FreshFontWeights.semibold, color, family: display),
      headlineMedium: _style(FreshTypeScale.xl2, FreshFontWeights.semibold, color, family: display),
      headlineSmall: _style(FreshTypeScale.xl, FreshFontWeights.semibold, color, family: display),
      titleLarge: _style(FreshTypeScale.xl, FreshFontWeights.semibold, color),
      titleMedium: _style(FreshTypeScale.lg, FreshFontWeights.semibold, color),
      titleSmall: _style(FreshTypeScale.sm, FreshFontWeights.semibold, color),
      bodyLarge: _style(FreshTypeScale.base, FreshFontWeights.regular, color),
      bodyMedium: _style(FreshTypeScale.sm, FreshFontWeights.regular, color),
      bodySmall: _style(FreshTypeScale.xs, FreshFontWeights.regular, color),
      labelLarge: _style(FreshTypeScale.sm, FreshFontWeights.semibold, color),
      labelMedium: _style(FreshTypeScale.xs, FreshFontWeights.semibold, color),
      labelSmall: _style(FreshTypeScale.xs, FreshFontWeights.medium, color),
    );
  }
}
