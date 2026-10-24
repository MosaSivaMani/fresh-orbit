// App-side theme refresh (D53): warm sand surfaces, brand teal as primary, a coral sticker accent, Baloo 2
// headings and Inter body (both OFL, bundled in assets/fonts). It extends `fresh_theme` from inside the app;
// `packages/flutter/fresh_theme` is generated from the design tokens and is not edited here.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// Font families declared in pubspec.yaml.
abstract final class AppFonts {
  static const String body = 'Inter';
  static const String display = 'Baloo2';
}

/// Colours the illustrated UI adds on top of [FreshColors]. Registered as a [ThemeExtension].
@immutable
class IllustratedColors extends ThemeExtension<IllustratedColors> {
  const IllustratedColors({
    required this.sand,
    required this.sandDeep,
    required this.paper,
    required this.coral,
    required this.onCoral,
    required this.ink,
    required this.tealText,
    required this.tealDecor,
    required this.tealSoft,
    required this.softShadow,
  });

  /// Scaffold background.
  final Color sand;

  /// Illustration circles, chips, skeletons.
  final Color sandDeep;

  /// Cards, receipt and polaroid paper.
  final Color paper;

  /// Sticker labels (decoration; the text on it uses [onCoral]).
  final Color coral;
  final Color onCoral;

  /// Thick outlines and offset shadows of stickers and polaroids.
  final Color ink;

  /// Brand teal for text-bearing elements (`#0B7D70` in light, D32).
  final Color tealText;

  /// Brand teal for decoration only (`#0E9384`); never behind or as text.
  final Color tealDecor;

  /// Pale teal for circles behind illustrations.
  final Color tealSoft;

  /// Soft card shadow.
  final Color softShadow;

  static const light = IllustratedColors(
    sand: Color(0xFFFBF4EA),
    sandDeep: Color(0xFFF2E3CC),
    paper: Color(0xFFFFFDF8),
    coral: Color(0xFFF7836B),
    onCoral: Color(0xFF2B140D),
    ink: Color(0xFF1D2B2E),
    tealText: Color(0xFF0B7D70),
    tealDecor: Color(0xFF0E9384),
    tealSoft: Color(0xFFD9F0EB),
    softShadow: Color(0x1F5C4426),
  );

  /// Deliberate dark mode: a warm night sand instead of the generated blue-teal dark, so the illustrations
  /// keep their warmth; outlines turn light so stickers stay visible.
  static const dark = IllustratedColors(
    sand: Color(0xFF17130F),
    sandDeep: Color(0xFF2E261E),
    paper: Color(0xFF221D18),
    coral: Color(0xFFFF9C85),
    onCoral: Color(0xFF2B140D),
    ink: Color(0xFFEFE3D1),
    tealText: Color(0xFF6FD6C7),
    tealDecor: Color(0xFF3CC2B1),
    tealSoft: Color(0xFF173A35),
    softShadow: Color(0x66000000),
  );

  @override
  IllustratedColors copyWith({
    Color? sand,
    Color? sandDeep,
    Color? paper,
    Color? coral,
    Color? onCoral,
    Color? ink,
    Color? tealText,
    Color? tealDecor,
    Color? tealSoft,
    Color? softShadow,
  }) => IllustratedColors(
    sand: sand ?? this.sand,
    sandDeep: sandDeep ?? this.sandDeep,
    paper: paper ?? this.paper,
    coral: coral ?? this.coral,
    onCoral: onCoral ?? this.onCoral,
    ink: ink ?? this.ink,
    tealText: tealText ?? this.tealText,
    tealDecor: tealDecor ?? this.tealDecor,
    tealSoft: tealSoft ?? this.tealSoft,
    softShadow: softShadow ?? this.softShadow,
  );

  @override
  IllustratedColors lerp(covariant ThemeExtension<IllustratedColors>? other, double t) {
    if (other is! IllustratedColors) return this;
    return IllustratedColors(
      sand: Color.lerp(sand, other.sand, t)!,
      sandDeep: Color.lerp(sandDeep, other.sandDeep, t)!,
      paper: Color.lerp(paper, other.paper, t)!,
      coral: Color.lerp(coral, other.coral, t)!,
      onCoral: Color.lerp(onCoral, other.onCoral, t)!,
      ink: Color.lerp(ink, other.ink, t)!,
      tealText: Color.lerp(tealText, other.tealText, t)!,
      tealDecor: Color.lerp(tealDecor, other.tealDecor, t)!,
      tealSoft: Color.lerp(tealSoft, other.tealSoft, t)!,
      softShadow: Color.lerp(softShadow, other.softShadow, t)!,
    );
  }
}

IllustratedColors illustratedColors(BuildContext context) =>
    Theme.of(context).extension<IllustratedColors>() ?? IllustratedColors.light;

/// Corner radii of the illustrated UI.
abstract final class AppRadius {
  static const double field = 16;
  static const double card = 22;
  static const double hero = 28;
  static const double sheet = 28;
}

abstract final class AppTheme {
  static ThemeData light() => _build(FreshTheme.light(), IllustratedColors.light, Brightness.light);

  static ThemeData dark() => _build(FreshTheme.dark(), IllustratedColors.dark, Brightness.dark);

  static ThemeData _build(ThemeData base, IllustratedColors ill, Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final generated = base.extension<FreshColors>()!;
    // Sand surfaces replace the generated cool greys; semantic colours (success, danger, …) stay generated.
    final fresh = generated.copyWith(
      background: ill.sand,
      surface: ill.paper,
      surfaceRaised: ill.paper,
      muted: ill.sandDeep,
      text: dark ? const Color(0xFFF6EEE3) : const Color(0xFF1D2B2E),
      textMuted: dark ? const Color(0xFFCBBBA6) : const Color(0xFF5D5A52),
      border: dark ? const Color(0xFF3B3229) : const Color(0xFFE8D9C3),
      borderStrong: dark ? const Color(0xFF8C7C69) : const Color(0xFF8A7B66),
      primary: dark ? const Color(0xFF3CC2B1) : ill.tealText,
      primaryHover: dark ? const Color(0xFF5CCFC0) : const Color(0xFF096A5F),
      onPrimary: dark ? const Color(0xFF052320) : Colors.white,
      accent: dark ? const Color(0xFF3CC2B1) : ill.tealText,
      accentSubtle: ill.tealSoft,
      accentText: ill.tealText,
      ring: ill.tealDecor,
      highlight: ill.coral,
      onHighlight: ill.onCoral,
    );
    final scheme = base.colorScheme.copyWith(
      primary: fresh.primary,
      onPrimary: fresh.onPrimary,
      primaryContainer: ill.tealSoft,
      onPrimaryContainer: ill.tealText,
      secondary: ill.coral,
      onSecondary: ill.onCoral,
      secondaryContainer: ill.sandDeep,
      onSecondaryContainer: fresh.text,
      surface: ill.paper,
      onSurface: fresh.text,
      onSurfaceVariant: fresh.textMuted,
      surfaceContainerLowest: ill.paper,
      surfaceContainerLow: ill.paper,
      surfaceContainer: ill.sand,
      surfaceContainerHigh: ill.sandDeep,
      surfaceContainerHighest: ill.sandDeep,
      outline: fresh.borderStrong,
      outlineVariant: fresh.border,
      surfaceTint: Colors.transparent,
    );
    final text = _textTheme(fresh.text);
    const pill = StadiumBorder();
    final fieldRadius = BorderRadius.circular(AppRadius.field);
    const buttonPadding = EdgeInsets.symmetric(horizontal: FreshSpacing.s6, vertical: FreshSpacing.s3);

    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: ill.sand,
      canvasColor: ill.sand,
      textTheme: text,
      primaryTextTheme: text,
      extensions: <ThemeExtension<dynamic>>[fresh, ill],
      dividerTheme: DividerThemeData(color: fresh.border, thickness: 1, space: 1),
      cardTheme: CardThemeData(
        color: ill.paper,
        elevation: 3,
        shadowColor: ill.softShadow,
        surfaceTintColor: Colors.transparent,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.card)),
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: ill.sand,
        foregroundColor: fresh.text,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: text.titleLarge,
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: ill.paper,
        surfaceTintColor: Colors.transparent,
        elevation: 3,
        shadowColor: ill.softShadow,
        indicatorColor: ill.tealSoft,
        indicatorShape: pill,
        labelTextStyle: WidgetStatePropertyAll(text.labelMedium),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: fresh.primary,
          foregroundColor: fresh.onPrimary,
          minimumSize: const Size(48, 52),
          padding: buttonPadding,
          shape: pill,
          textStyle: text.labelLarge?.copyWith(fontSize: 16),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: fresh.text,
          minimumSize: const Size(48, 52),
          padding: buttonPadding,
          side: BorderSide(color: fresh.borderStrong, width: 1.5),
          shape: pill,
          textStyle: text.labelLarge?.copyWith(fontSize: 16),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: ill.tealText,
          minimumSize: const Size(48, 48),
          shape: pill,
          textStyle: text.labelLarge,
        ),
      ),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: fresh.primary,
        foregroundColor: fresh.onPrimary,
        shape: pill,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: ill.paper,
        contentPadding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s4, vertical: FreshSpacing.s4),
        border: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: fresh.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: fresh.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: fresh.ring, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: fresh.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: fresh.danger, width: 2),
        ),
        labelStyle: TextStyle(color: fresh.textMuted),
        hintStyle: TextStyle(color: fresh.textMuted),
        errorStyle: TextStyle(color: fresh.dangerText),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: ill.paper,
        selectedColor: ill.tealSoft,
        labelStyle: text.labelLarge,
        side: BorderSide(color: fresh.border),
        shape: pill,
        padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s2, vertical: FreshSpacing.s1),
      ),
      listTileTheme: ListTileThemeData(
        iconColor: ill.tealText,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.field)),
      ),
      expansionTileTheme: ExpansionTileThemeData(
        iconColor: ill.tealText,
        collapsedIconColor: fresh.textMuted,
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
        titleTextStyle: text.titleLarge,
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: fresh.text,
        contentTextStyle: text.bodyMedium?.copyWith(color: ill.sand),
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.field)),
      ),
      badgeTheme: BadgeThemeData(backgroundColor: ill.coral, textColor: ill.onCoral),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: fresh.primary),
    );
  }

  static TextStyle _style(double size, double height, FontWeight weight, Color color, String family) => TextStyle(
    fontFamily: family,
    fontFamilyFallback: FreshFonts.sansFallback,
    fontSize: size,
    height: height,
    fontWeight: weight,
    color: color,
  );

  /// Baloo 2 for display, headline and the two larger titles (chunky, rounded); Inter for everything people read.
  static TextTheme _textTheme(Color color) {
    const d = AppFonts.display;
    const b = AppFonts.body;
    return TextTheme(
      displayLarge: _style(48, 1.1, FontWeight.w800, color, d),
      displayMedium: _style(40, 1.1, FontWeight.w800, color, d),
      displaySmall: _style(34, 1.12, FontWeight.w800, color, d),
      headlineLarge: _style(32, 1.15, FontWeight.w800, color, d),
      headlineMedium: _style(28, 1.18, FontWeight.w700, color, d),
      headlineSmall: _style(24, 1.2, FontWeight.w700, color, d),
      titleLarge: _style(21, 1.25, FontWeight.w700, color, d),
      titleMedium: _style(18, 1.3, FontWeight.w700, color, d),
      titleSmall: _style(14, 1.4, FontWeight.w600, color, b),
      bodyLarge: _style(16, 1.5, FontWeight.w400, color, b),
      bodyMedium: _style(14, 1.45, FontWeight.w400, color, b),
      bodySmall: _style(12, 1.35, FontWeight.w400, color, b),
      labelLarge: _style(14, 1.3, FontWeight.w600, color, b),
      labelMedium: _style(12, 1.3, FontWeight.w600, color, b),
      labelSmall: _style(11, 1.3, FontWeight.w500, color, b),
    );
  }
}
