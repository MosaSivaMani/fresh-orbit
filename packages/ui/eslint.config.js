import react from "@fresh/config/eslint/react";

export default [
  ...react,
  {
    // A component library has no HMR boundary; mixed exports (components + helpers) are intended.
    rules: { "react-refresh/only-export-components": "off" },
  },
];
