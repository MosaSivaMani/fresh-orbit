import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: { port: 5199 },
  // Not deployed; one bundle is fine.
  build: { chunkSizeWarningLimit: 1000 },
});
