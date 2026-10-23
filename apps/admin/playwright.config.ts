import { defineConfig, devices } from "@playwright/test";

// Smoke specs run against `vite preview` of the built admin, without the API. Full journeys (pack, receive
// stock, price change, city activation) need the API with sample data and are added after Phase 3.
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: { baseURL: "http://localhost:4173", trace: "on-first-retry" },
  webServer: {
    command: "pnpm build && pnpm preview",
    url: "http://localhost:4173",
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
