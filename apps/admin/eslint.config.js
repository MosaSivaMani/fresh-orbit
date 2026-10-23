import react from "@fresh/config/eslint/react";

export default [
  ...react,
  { ignores: ["playwright-report/**", "test-results/**"] },
  {
    // Hooks, providers and route lists live next to the components they serve; fast refresh of those files
    // reloads the page, which is fine.
    files: [
      "src/app/**",
      "src/lib/**",
      "src/components/**",
      "src/api/**",
      "src/test/**",
      "**/routes.tsx",
      "src/features/*/shared.tsx",
    ],
    rules: { "react-refresh/only-export-components": "off" },
  },
];
