// Next.js ESLint flat preset: react preset + @next/eslint-plugin-next (recommended + core web vitals).
import nextPlugin from "@next/eslint-plugin-next";
import tseslint from "typescript-eslint";
import react from "./react.js";

/** @type {import("typescript-eslint").ConfigArray} */
const next = tseslint.config(...react, {
  files: ["**/*.{js,jsx,ts,tsx}"],
  plugins: { "@next/next": nextPlugin },
  rules: {
    ...nextPlugin.configs.recommended.rules,
    ...nextPlugin.configs["core-web-vitals"].rules,
    // Next.js pages and layouts export metadata alongside components.
    "react-refresh/only-export-components": "off",
  },
});

export default next;
