import { expect, test } from "@playwright/test";

test("login page renders", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByRole("heading", { name: "Sign in to Fresh Orbit Admin" })).toBeVisible();
  await expect(page.getByLabel("Email")).toBeVisible();
  await expect(page.getByLabel("Password")).toBeVisible();
  await expect(page.getByRole("button", { name: "Sign in" })).toBeVisible();
});

test("login side panel loads the ADMIN_LOGIN illustration and the self-hosted display font", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto("/login");
  const picture = page.locator('img[data-slot="ADMIN_LOGIN"]');
  await expect(picture).toBeVisible();
  await expect
    .poll(() => picture.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0))
    .toBe(true);
  // The family comes from the token, so this test survives a font swap.
  const family = await page.evaluate(() =>
    getComputedStyle(document.documentElement)
      .getPropertyValue("--fo-font-display")
      .split(",")[0]!
      .trim()
      .replace(/^"|"$/g, ""),
  );
  await expect
    .poll(() =>
      page.evaluate(async (f) => {
        await document.fonts.load(`700 16px "${f}"`);
        return document.fonts.check(`700 16px "${f}"`);
      }, family),
    )
    .toBe(true);
});

test("signed-out visitors are sent to the login page", async ({ page }) => {
  await page.goto("/orders");
  await expect(page).toHaveURL(/\/login/);
});
