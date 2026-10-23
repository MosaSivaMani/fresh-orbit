// React ESLint flat preset: base + react-hooks + jsx-a11y (strict) + react-refresh (warn).
import jsxA11y from "eslint-plugin-jsx-a11y";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import base from "./base.js";

/** @type {import("typescript-eslint").ConfigArray} */
const react = tseslint.config(...base, {
  files: ["**/*.{jsx,tsx}"],
  plugins: {
    "react-hooks": reactHooks,
    "jsx-a11y": jsxA11y,
    "react-refresh": reactRefresh,
  },
  languageOptions: {
    parserOptions: { ecmaFeatures: { jsx: true } },
  },
  rules: {
    ...reactHooks.configs.recommended.rules,
    ...jsxA11y.flatConfigs.strict.rules,
    "react-refresh/only-export-components": ["warn", { allowConstantExport: true }],
  },
});

export default react;
