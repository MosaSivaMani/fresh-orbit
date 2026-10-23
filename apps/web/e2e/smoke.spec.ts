import { expect, test } from "@playwright/test";

test("home page renders its shell and can be navigated by keyboard", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveTitle(/Fresh Orbit/);
  await expect(page.getByRole("banner")).toBeVisible();
  await expect(page.getByRole("main")).toBeVisible();
  await expect(page.getByRole("contentinfo")).toBeVisible();

  // The first Tab lands on the skip link.
  await page.keyboard.press("Tab");
  await expect(page.getByRole("link", { name: /skip to content/i })).toBeFocused();
});

test("unknown pages show the not-found page", async ({ page }) => {
  const response = await page.goto("/this-page-does-not-exist");
  expect(response?.status()).toBe(404);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});
