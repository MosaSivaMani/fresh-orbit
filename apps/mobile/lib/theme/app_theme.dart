// App theme, look "Bold blocks" (D88, reference docs/design/mockups/app-bold-blocks.html): a warm page, paper
// sheets and cards, ink text, and four strong block colours (coral, teal, mango, ice) for hero, empty and
// onboarding screens; Baloo 2 headings, Inter body and a handwritten Dancing Script heading line (D89). It
// extends `fresh_theme` from inside the app; `packages/flutter/fresh_theme` is generated from the design tokens
// and is not edited here.

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// Font families, one constant per role (D89 amended: Baloo 2 headings and Inter body, both bundled in
/// `assets/fonts/`; Dancing Script for the handwritten second line of big headings). Nothing else in the app
/// names a family.
abstract final class AppFonts {
  /// Headings, prices, counts, stickers: Baloo 2 800.
  static const String display = 'Baloo2';

  /// Everything people read, labels and buttons.
  static const String body = 'Inter';

  /// The handwritten lines (Dancing Script 700): bundled as the variable font
  /// `assets/fonts/DancingScript-Variable.ttf`; [scriptStyle] sets the 700 weight on its `wght` axis
  /// (docs/design/mobile.md §15).
  static const String script = 'DancingScript';
}

/// Colours of the Bold blocks look, registered as a [ThemeExtension] (read with [illustratedColors]). Field
/// names follow the mockup's CSS tokens: `sand` is `--page`, `tealText` is `--teal`.
@immutable
class IllustratedColors extends ThemeExtension<IllustratedColors> {
  const IllustratedColors({
    required this.sand,
    required this.sandDeep,
    required this.paper,
    required this.line,
    required this.ink,
    required this.onInk,
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
    required this.mangoSoft,
    required this.ice,
    required this.iceSoft,
    required this.softShadow,
  });

  /// Page background (`--page`).
  final Color sand;

  /// Stronger page tone: skeletons, segmented-control track.
  final Color sandDeep;

  /// Sheets, cards, the tab bar (`--paper`).
  final Color paper;

  /// Hairlines, dashed rules, idle chip borders (`--line`).
  final Color line;

  /// Text and the dark primary button (`--ink`); [onInk] is text on it.
  final Color ink;
  final Color onInk;
  final Color muted;

  /// Brand teal (`--teal`): the active tab, the cart bar, Place order, the teal block.
  final Color tealText;

  /// Teal for text on paper and on [tealSoft] (`--teal-deep`).
  final Color tealDeep;

  /// Text on teal. Paper in light; in dark the brighter teal needs ink-dark text to keep AA contrast.
  final Color onTeal;
  final Color tealDecor;
  final Color tealSoft;

  final Color coral;
  final Color onCoral;
  final Color coralText;
  final Color coralSoft;

  final Color mango;
  final Color onMango;
  final Color mangoSoft;

  final Color ice;
  final Color iceSoft;

  /// Card shadow colour.
  final Color softShadow;

  static const light = IllustratedColors(
    sand: Color(0xFFF4EEE3),
    sandDeep: Color(0xFFEADFCC),
    paper: Color(0xFFFFFDF8),
    line: Color(0xFFE8DCC8),
    ink: Color(0xFF14302F),
    onInk: Color(0xFFFFFDF8),
    muted: Color(0xFF5B6F6B),
    tealText: Color(0xFF0B7D70),
    tealDeep: Color(0xFF075F55),
    onTeal: Color(0xFFFFFDF8),
    tealDecor: Color(0xFF0B7D70),
    tealSoft: Color(0xFFD8EEEA),
    coral: Color(0xFFF07A5A),
    onCoral: Color(0xFF1D0F0A),
    coralText: Color(0xFFB4432A),
    coralSoft: Color(0xFFFCE3DA),
    mango: Color(0xFFF5B544),
    onMango: Color(0xFF14302F),
    mangoSoft: Color(0xFFFBEBC8),
    ice: Color(0xFFBFE3EE),
    iceSoft: Color(0xFFE4F3F8),
    softShadow: Color(0x24281E0A),
  );

  static const dark = IllustratedColors(
    sand: Color(0xFF101918),
    sandDeep: Color(0xFF1C2C2A),
    paper: Color(0xFF1A2826),
    line: Color(0xFF2C3E3B),
    ink: Color(0xFFF4EBDD),
    onInk: Color(0xFF0C1B1A),
    muted: Color(0xFFA9B8B3),
    tealText: Color(0xFF2FA595),
    tealDeep: Color(0xFF7FD9CB),
    onTeal: Color(0xFF0C1B1A),
    tealDecor: Color(0xFF2FA595),
    tealSoft: Color(0xFF16413C),
    coral: Color(0xFFE0714F),
    onCoral: Color(0xFF1D0F0A),
    coralText: Color(0xFFF7A68E),
    coralSoft: Color(0xFF3D2620),
    mango: Color(0xFFE0A43C),
    onMango: Color(0xFF14302F),
    mangoSoft: Color(0xFF3A2F17),
    ice: Color(0xFF4D93A6),
    iceSoft: Color(0xFF1D3940),
    softShadow: Color(0x66000000),
  );

  @override
  IllustratedColors copyWith({Color? sand, Color? paper, Color? ink, Color? tealText, Color? coral}) =>
      IllustratedColors(
        sand: sand ?? this.sand,
        sandDeep: sandDeep,
        paper: paper ?? this.paper,
        line: line,
        ink: ink ?? this.ink,
        onInk: onInk,
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
        mangoSoft: mangoSoft,
        ice: ice,
        iceSoft: iceSoft,
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
      onInk: l(onInk, other.onInk),
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
      mangoSoft: l(mangoSoft, other.mangoSoft),
      ice: l(ice, other.ice),
      iceSoft: l(iceSoft, other.iceSoft),
      softShadow: l(softShadow, other.softShadow),
    );
  }
}

IllustratedColors illustratedColors(BuildContext context) =>
    Theme.of(context).extension<IllustratedColors>() ?? IllustratedColors.light;

/// Colours that stay the same in light and dark, as in the mockup: the ink of avatar rings and stickers, and
/// the paper of avatar borders and paper stickers.
abstract final class BlockInk {
  static const Color ink = Color(0xFF14302F);
  static const Color paper = Color(0xFFFFFDF8);
}

/// Corner radii (mockup: sheets 34, cards 20, buttons 20, fields 16).
abstract final class AppRadius {
  static const double field = 16;
  static const double card = 20;
  static const double hero = 24;
  static const double sheet = 34;
  static const double button = 20;
}

/// Card shadow of the mockup (`--shadow`).
List<BoxShadow> cardShadow(BuildContext context) {
  final dark = Theme.of(context).brightness == Brightness.dark;
  return [
    BoxShadow(
      color: dark ? const Color(0x4D000000) : const Color(0x14281E0A),
      blurRadius: 4,
      offset: const Offset(0, 2),
    ),
    BoxShadow(color: illustratedColors(context).softShadow, blurRadius: 30, offset: const Offset(0, 14)),
  ];
}

/// Text styles the mockup uses outside the Material text theme.
abstract final class AppText {
  /// Display letter spacing (−0.01em, D89 amended).
  static double tight(double size) => -0.01 * size;

  /// The big headline on a colour block (Baloo 2 800, 36).
  static TextStyle blockTitle(Color color) => TextStyle(
    fontFamily: AppFonts.display,
    fontFamilyFallback: FreshFonts.sansFallback,
    fontWeight: FontWeight.w800,
    fontSize: 36,
    height: 1.0,
    letterSpacing: tight(36),
    color: color,
  );

  /// Small caps label (Inter 700, 12, +0.1em): "STEP 1 OF 3", "DELIVERY AREA".
  static TextStyle eyebrow(Color color, {double size = 12}) => TextStyle(
    fontFamily: AppFonts.body,
    fontFamilyFallback: FreshFonts.sansFallback,
    fontWeight: FontWeight.w700,
    fontSize: size,
    height: 1.3,
    letterSpacing: 0.1 * size,
    color: color,
  );

  /// Prices and counts: display face, tabular figures.
  static TextStyle price(Color color, {double size = 15}) => TextStyle(
    fontFamily: AppFonts.display,
    fontFamilyFallback: FreshFonts.sansFallback,
    fontWeight: FontWeight.w800,
    fontSize: size,
    height: 1.15,
    letterSpacing: tight(size) * 0.8,
    color: color,
    fontFeatures: const [FontFeature.tabularFigures()],
  );

  /// A display-face title of any size (Baloo 2 800, tight).
  static TextStyle display(Color color, double size, {FontWeight weight = FontWeight.w800, double height = 1.05}) =>
      TextStyle(
        fontFamily: AppFonts.display,
        fontFamilyFallback: FreshFonts.sansFallback,
        fontWeight: weight,
        fontSize: size,
        height: height,
        letterSpacing: tight(size),
        color: color,
      );
}

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
