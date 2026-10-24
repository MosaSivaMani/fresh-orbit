{{flutter_js}}
{{flutter_build_config}}

// Same-origin only: CanvasKit is bundled (--no-web-resources-cdn) and fallback fonts are not fetched from a
// font CDN. Inter and Baloo 2 ship with the app; a glyph they lack shows as a box instead of a network call.
_flutter.loader.load({
  config: {
    fontFallbackBaseUrl: 'assets/fallback-fonts/',
  },
});
