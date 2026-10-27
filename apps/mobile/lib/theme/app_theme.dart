// App theme, look "Bold blocks" (D88, reference docs/design/mockups/app-bold-blocks.html): a warm page, paper
// sheets and cards, ink text, and four strong block colours (coral, teal, mango, ice) for hero, empty and
// onboarding screens; Baloo 2 headings, Inter body and a handwritten Dancing Script heading line (D89). It
// extends `fresh_theme` from inside the app; `packages/flutter/fresh_theme` is generated from the design tokens
// and is not edited here.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

abstract final class AppTheme {
  static ThemeData light() => _build(FreshTheme.light(), IllustratedColors.light, Brightness.light);

  static ThemeData dark() => _build(FreshTheme.dark(), IllustratedColors.dark, Brightness.dark);

  static ThemeData _build(ThemeData base, IllustratedColors ill, Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final generated = base.extension<FreshColors>()!;
    // Page and paper replace the generated cool greys; semantic colours (success, danger, …) stay generated.
    final fresh = generated.copyWith(
      background: ill.sand,
      surface: ill.paper,
      surfaceRaised: ill.paper,
      muted: ill.sandDeep,
      text: ill.ink,
      textMuted: ill.muted,
      border: ill.line,
      borderStrong: dark ? const Color(0xFF5C7570) : const Color(0xFF8A7B66),
      primary: ill.tealText,
      primaryHover: ill.tealDeep,
      onPrimary: ill.onTeal,
      accent: ill.tealText,
      accentSubtle: ill.tealSoft,
      accentText: ill.tealDeep,
      ring: ill.coral,
      highlight: ill.mango,
      onHighlight: ill.onMango,
      highlightSubtle: ill.mangoSoft,
      highlightText: ill.coralText,
    );
    final scheme = base.colorScheme.copyWith(
      primary: ill.tealText,
      onPrimary: ill.onTeal,
      primaryContainer: ill.tealSoft,
      onPrimaryContainer: ill.tealDeep,
      secondary: ill.coral,
      onSecondary: ill.onCoral,
      secondaryContainer: ill.mangoSoft,
      onSecondaryContainer: ill.ink,
      surface: ill.paper,
      onSurface: ill.ink,
      onSurfaceVariant: ill.muted,
      surfaceContainerLowest: ill.paper,
      surfaceContainerLow: ill.paper,
      surfaceContainer: ill.sand,
      surfaceContainerHigh: ill.sandDeep,
      surfaceContainerHighest: ill.sandDeep,
      outline: fresh.borderStrong,
      outlineVariant: ill.line,
      inverseSurface: ill.ink,
      onInverseSurface: ill.onInk,
      surfaceTint: Colors.transparent,
    );
    final text = _textTheme(ill.ink);
    final buttonShape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.button));
    final fieldRadius = BorderRadius.circular(AppRadius.field);
    const buttonPadding = EdgeInsets.symmetric(horizontal: FreshSpacing.s5, vertical: FreshSpacing.s3);
    final buttonText = text.labelLarge?.copyWith(fontSize: 15, fontWeight: FontWeight.w700, letterSpacing: -0.15);

    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: ill.sand,
      canvasColor: ill.sand,
      textTheme: text,
      primaryTextTheme: text,
      extensions: <ThemeExtension<dynamic>>[fresh, ill],
      dividerTheme: DividerThemeData(color: ill.line, thickness: 1, space: 1),
      cardTheme: CardThemeData(
        color: ill.paper,
        elevation: 2,
        shadowColor: ill.softShadow,
        surfaceTintColor: Colors.transparent,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.card)),
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: ill.sand,
        foregroundColor: ill.ink,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: AppText.display(ill.ink, 21),
      ),
      // The dark ink button of the mockup (`.btn`). Teal actions (Place order, the cart bar) style themselves.
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: ill.ink,
          foregroundColor: ill.onInk,
          disabledBackgroundColor: ill.line,
          disabledForegroundColor: ill.muted,
          minimumSize: const Size(48, 52),
          padding: buttonPadding,
          shape: buttonShape,
          textStyle: buttonText,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: ill.ink,
          minimumSize: const Size(48, 52),
          padding: buttonPadding,
          side: BorderSide(color: ill.line, width: 2),
          shape: buttonShape,
          textStyle: buttonText,
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: ill.tealDeep,
          minimumSize: const Size(48, 48),
          shape: const StadiumBorder(),
          textStyle: text.labelLarge?.copyWith(fontWeight: FontWeight.w700),
        ),
      ),
      iconButtonTheme: IconButtonThemeData(style: IconButton.styleFrom(foregroundColor: ill.ink)),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: ill.tealText,
        foregroundColor: ill.onTeal,
        shape: const StadiumBorder(),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: ill.paper,
        contentPadding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s4, vertical: FreshSpacing.s4),
        border: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: ill.line, width: 1.5),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: ill.line, width: 1.5),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: ill.ink, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: fresh.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: fresh.danger, width: 2),
        ),
        labelStyle: TextStyle(color: ill.muted),
        hintStyle: TextStyle(color: ill.muted),
        errorStyle: TextStyle(color: fresh.dangerText),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: ill.paper,
        selectedColor: ill.ink,
        secondarySelectedColor: ill.ink,
        checkmarkColor: ill.onInk,
        labelStyle: text.labelLarge?.copyWith(fontWeight: FontWeight.w700),
        secondaryLabelStyle: text.labelLarge?.copyWith(fontWeight: FontWeight.w700, color: ill.onInk),
        side: BorderSide(color: ill.line, width: 1.5),
        shape: const StadiumBorder(),
        padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s2, vertical: FreshSpacing.s1),
      ),
      switchTheme: SwitchThemeData(
        thumbColor: const WidgetStatePropertyAll(BlockInk.paper),
        trackColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? ill.tealText : ill.line),
        trackOutlineColor: const WidgetStatePropertyAll(Colors.transparent),
      ),
      radioTheme: RadioThemeData(
        fillColor: WidgetStateProperty.resolveWith((s) => s.contains(WidgetState.selected) ? ill.tealText : ill.muted),
      ),
      listTileTheme: ListTileThemeData(
        iconColor: ill.tealText,
        titleTextStyle: text.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.field)),
      ),
      expansionTileTheme: ExpansionTileThemeData(
        iconColor: ill.tealText,
        collapsedIconColor: ill.muted,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.field)),
        collapsedShape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.field)),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: ill.paper,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadius.sheet))),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: ill.paper,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.hero)),
        titleTextStyle: AppText.display(ill.ink, 22),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: ill.ink,
        contentTextStyle: text.bodyMedium?.copyWith(color: ill.onInk),
        actionTextColor: ill.mango,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.field)),
      ),
      badgeTheme: BadgeThemeData(backgroundColor: ill.coral, textColor: ill.onCoral),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: ill.tealText),
    );
  }

  static TextStyle _style(double size, double height, FontWeight weight, Color color, String family) => TextStyle(
    fontFamily: family,
    fontFamilyFallback: FreshFonts.sansFallback,
    fontSize: size,
    height: height,
    fontWeight: weight,
    color: color,
    letterSpacing: family == AppFonts.display ? AppText.tight(size) : null,
  );

  /// Display face (800, tight) for display, headline and the two larger titles; body face for everything
  /// people read.
  static TextTheme _textTheme(Color color) {
    const d = AppFonts.display;
    const b = AppFonts.body;
    return TextTheme(
      displayLarge: _style(48, 1.0, FontWeight.w800, color, d),
      displayMedium: _style(40, 1.0, FontWeight.w800, color, d),
      displaySmall: _style(36, 1.0, FontWeight.w800, color, d),
      headlineLarge: _style(32, 1.05, FontWeight.w800, color, d),
      headlineMedium: _style(27, 1.05, FontWeight.w800, color, d),
      headlineSmall: _style(24, 1.1, FontWeight.w800, color, d),
      titleLarge: _style(20, 1.15, FontWeight.w800, color, d),
      titleMedium: _style(17, 1.2, FontWeight.w800, color, d),
      titleSmall: _style(14, 1.4, FontWeight.w700, color, b),
      bodyLarge: _style(16, 1.5, FontWeight.w400, color, b),
      bodyMedium: _style(14, 1.45, FontWeight.w400, color, b),
      bodySmall: _style(12, 1.35, FontWeight.w400, color, b),
      labelLarge: _style(14, 1.3, FontWeight.w600, color, b),
      labelMedium: _style(12, 1.3, FontWeight.w600, color, b),
      labelSmall: _style(11, 1.3, FontWeight.w500, color, b),
    );
  }
}
