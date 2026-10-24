import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Dev: the API runs on :5080 (`dotnet run --project backend/src/Hosts/FreshOrbit.Api`); /api and /media are
// proxied so the admin and the API share one origin. Production: serve both behind Caddy, or set VITE_API_ORIGIN.
const apiTarget = process.env.FRESH_API_PROXY ?? "http://localhost:5080";
// In a GitHub Codespace the dev server is opened through a forwarded https URL (`<name>-5173.<domain>`). Dev server
// only; the API keeps its CORS list because the browser only ever talks to this origin.
const codespacesDomain = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN?.trim();

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    ...(codespacesDomain ? { allowedHosts: [`.${codespacesDomain}`] } : {}),
    proxy: {
      "/api": { target: apiTarget, changeOrigin: true },
      "/media": { target: apiTarget, changeOrigin: true },
    },
  },
  preview: { port: 4173 },
  build: { chunkSizeWarningLimit: 900 },
});
