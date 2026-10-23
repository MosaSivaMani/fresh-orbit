import { defineConfig } from "tsup";

export default defineConfig({
  entry: ["src/index.ts"],
  format: ["esm"],
  target: "es2022",
  dts: true,
  sourcemap: true,
  clean: true,
  external: ["react", "react-dom", "react/jsx-runtime", "@fresh/core"],
  // Components use hooks and browser APIs; mark the bundle as a client module for Next.js App Router.
  banner: { js: '"use client";' },
});
