import { defineConfig, devices } from "@playwright/test";

// End-to-end smoke tests against a production build. Run with `pnpm --filter @fresh/web test:e2e` after
// `pnpm exec playwright install chromium`. Not part of `pnpm test` (unit tests) yet; see docs/design/web.md.
const PORT = Number(process.env.E2E_PORT ?? 3100);
const baseURL = process.env.E2E_BASE_URL ?? `http://localhost:${PORT}`;

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: { baseURL, trace: "on-first-retry" },
  projects: [
    { name: "desktop-chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "mobile-chromium", use: { ...devices["Pixel 7"] } },
  ],
  ...(process.env.E2E_BASE_URL
    ? {}
    : {
        webServer: {
          command: `pnpm build && pnpm exec next start -p ${PORT}`,
          url: baseURL,
          timeout: 180_000,
          reuseExistingServer: !process.env.CI,
          env: {
            // Point at a running API for full flows; the smoke test only needs the page shell to render.
            FRESH_API_ORIGIN: process.env.FRESH_API_ORIGIN ?? "http://127.0.0.1:5080",
            NEXT_PUBLIC_SITE_URL: baseURL,
          },
        },
      }),
});
