import { expect, test } from "@playwright/test";

// The one thing every real account does before anything else in Natoshare makes
// sense (see the onboarding wizard's own comment): sign up, set up currency and
// categories, and land on a working dashboard. This walks that path against a real
// running API and a real Postgres database, start to finish, in a real browser.
test("a brand new visitor can sign up, finish onboarding, and see their dashboard", async ({ page }) => {
  const email = `e2e-${Date.now()}@example.com`;

  await page.goto("/signup");
  await page.fill("#displayName", "E2E Tester");
  await page.fill("#email", email);
  await page.fill("#password", "correct-horse-1");
  await page.click('button[type="submit"]');

  await page.waitForURL("**/onboarding");
  await expect(page.getByText("Where should we set you up?")).toBeVisible();
  await page.getByRole("button", { name: "Continue" }).click();

  await expect(page.getByText("What's your monthly budget?")).toBeVisible();
  await page.fill("#income", "500000");

  // Any seeded template sums its own categories to 100% already, so picking one
  // is the fastest reliable way to reach a submittable state.
  await page.locator("button", { hasText: "50/30/20" }).first().click();

  await expect(page.getByText("100% allocated")).toBeVisible();
  await page.getByRole("button", { name: "Finish setup" }).click();

  await page.waitForURL("**/dashboard", { timeout: 15000 });
  await expect(page.getByText("Net position")).toBeVisible();
});
