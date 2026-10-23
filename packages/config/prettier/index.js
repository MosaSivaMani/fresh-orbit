// Shared Prettier preset. The Tailwind plugin sorts class names; it must stay the last plugin.
/** @type {import("prettier").Config} */
const config = {
  printWidth: 100,
  singleQuote: false,
  semi: true,
  trailingComma: "all",
  arrowParens: "always",
  plugins: ["prettier-plugin-tailwindcss"],
  tailwindFunctions: ["cn", "cva"],
};

export default config;
