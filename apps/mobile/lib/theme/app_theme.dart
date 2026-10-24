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

/// Colours the illustrated UI adds on top of [FreshColors] (the published style concept, D53). Registered as a
/// [ThemeExtension].
@immutable
class IllustratedColors extends ThemeExtension<IllustratedColors> {
  const IllustratedColors({
    required this.sand,
    required this.sandDeep,
    required this.paper,
    required this.line,
    required this.ink,
    required this.muted,
    required this.tealText,
    required this.tealDeep,
    required this.onTeal,
    required this.tealDecor,
    required this.tealSoft,
    required this.coral,
    required this.onCoral,
    required this.coralText,
    required this.coralSoft,
    required this.mango,
    required this.onMango,
    required this.ice,
    required this.softShadow,
  });

  /// Scaffold background.
  final Color sand;

  /// Stronger sand: illustration circles, chips, skeletons.
  final Color sandDeep;

  /// Cards, receipt and polaroid paper.
  final Color paper;

  /// Hairlines and dashed rules.
  final Color line;

  /// Text, thick outlines and the offset shadows of stickers and polaroids.
  final Color ink;
  final Color muted;

  /// Brand teal for anything you tap and for text (`#0B7D70` in light, D32).
  final Color tealText;
  final Color tealDeep;
  final Color onTeal;

  /// Brand teal for decoration only (`#0E9384`); never behind or as text.
  final Color tealDecor;

  /// Pale teal: circles behind illustrations, the receipt total band.
  final Color tealSoft;

  /// Sticker coral (decoration; text on it uses [onCoral]); [coralText] for coral words on sand or paper.
  final Color coral;
  final Color onCoral;
  final Color coralText;
  final Color coralSoft;

  /// Mango stickers.
  final Color mango;
  final Color onMango;

  /// Ice blue: the rider's cold box, cold-chain moments.
  final Color ice;

  /// Soft card shadow.
  final Color softShadow;

  static const light = IllustratedColors(
    sand: Color(0xFFFBF4E8),
    sandDeep: Color(0xFFF3E4CC),
    paper: Color(0xFFFFFDF8),
    line: Color(0xFFE6D7BE),
    ink: Color(0xFF14302F),
    muted: Color(0xFF5B6F6B),
    tealText: Color(0xFF0B7D70),
    tealDeep: Color(0xFF075F55),
    onTeal: Color(0xFFFFFDF8),
    tealDecor: Color(0xFF0E9384),
    tealSoft: Color(0xFFD8EEEA),
    coral: Color(0xFFF07A5A),
    onCoral: Color(0xFF1D0F0A),
    coralText: Color(0xFFB4432A),
    coralSoft: Color(0xFFFCE3DA),
    mango: Color(0xFFF5B544),
    onMango: Color(0xFF231707),
    ice: Color(0xFFDCEFF5),
    softShadow: Color(0x1F3C280A),
  );

  /// Deliberate dark mode: a deep sea-green night instead of a grey, ink turns light so stickers and polaroid
  /// outlines stay visible, and the brand teal brightens to keep its contrast.
  static const dark = IllustratedColors(
    sand: Color(0xFF14201F),
    sandDeep: Color(0xFF1C2C2A),
    paper: Color(0xFF1A2826),
    line: Color(0xFF2C3E3B),
    ink: Color(0xFFF4EBDD),
    muted: Color(0xFFA9B8B3),
    tealText: Color(0xFF3FC0AE),
    tealDeep: Color(0xFF2A9C8C),
    onTeal: Color(0xFF0C1B1A),
    tealDecor: Color(0xFF3FC0AE),
    tealSoft: Color(0xFF16413C),
    coral: Color(0xFFF28B6E),
    onCoral: Color(0xFF1D0F0A),
    coralText: Color(0xFFF7A68E),
    coralSoft: Color(0xFF3D2620),
    mango: Color(0xFFE9AE45),
    onMango: Color(0xFF231707),
    ice: Color(0xFF1D3940),
    softShadow: Color(0x59000000),
  );

  @override
  IllustratedColors copyWith({Color? sand, Color? paper, Color? ink, Color? tealText, Color? coral}) =>
      IllustratedColors(
        sand: sand ?? this.sand,
        sandDeep: sandDeep,
        paper: paper ?? this.paper,
        line: line,
        ink: ink ?? this.ink,
        muted: muted,
        tealText: tealText ?? this.tealText,
        tealDeep: tealDeep,
        onTeal: onTeal,
        tealDecor: tealDecor,
        tealSoft: tealSoft,
        coral: coral ?? this.coral,
        onCoral: onCoral,
        coralText: coralText,
        coralSoft: coralSoft,
        mango: mango,
        onMango: onMango,
        ice: ice,
        softShadow: softShadow,
      );

  @override
  IllustratedColors lerp(covariant ThemeExtension<IllustratedColors>? other, double t) {
    if (other is! IllustratedColors) return this;
    Color l(Color a, Color b) => Color.lerp(a, b, t)!;
    return IllustratedColors(
      sand: l(sand, other.sand),
      sandDeep: l(sandDeep, other.sandDeep),
      paper: l(paper, other.paper),
      line: l(line, other.line),
      ink: l(ink, other.ink),
      muted: l(muted, other.muted),
      tealText: l(tealText, other.tealText),
      tealDeep: l(tealDeep, other.tealDeep),
      onTeal: l(onTeal, other.onTeal),
      tealDecor: l(tealDecor, other.tealDecor),
      tealSoft: l(tealSoft, other.tealSoft),
      coral: l(coral, other.coral),
      onCoral: l(onCoral, other.onCoral),
      coralText: l(coralText, other.coralText),
      coralSoft: l(coralSoft, other.coralSoft),
      mango: l(mango, other.mango),
      onMango: l(onMango, other.onMango),
      ice: l(ice, other.ice),
      softShadow: l(softShadow, other.softShadow),
    );
  }
}

IllustratedColors illustratedColors(BuildContext context) =>
    Theme.of(context).extension<IllustratedColors>() ?? IllustratedColors.light;

/// Corner radii of the illustrated UI.
abstract final class AppRadius {
  static const double field = 16;
  static const double card = 24;
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
      text: ill.ink,
      textMuted: ill.muted,
      border: ill.line,
      borderStrong: dark ? const Color(0xFF5C7570) : const Color(0xFF8A7B66),
      primary: ill.tealText,
      primaryHover: ill.tealDeep,
      onPrimary: ill.onTeal,
      accent: ill.tealText,
      accentSubtle: ill.tealSoft,
      accentText: dark ? ill.tealText : ill.tealDeep,
      ring: ill.coral,
      highlight: ill.mango,
      onHighlight: ill.onMango,
      highlightSubtle: ill.coralSoft,
      highlightText: ill.coralText,
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
