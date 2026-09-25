import { expect, test } from "@playwright/test";

// A smoke test for the admin app: an admin can log in, see the users list, open one
// account's detail, and read the audit log, all against a real running API and a
// real Postgres database. This is deliberately read-only, it is a smoke test, not a
// full walk of every admin action (those are already covered by
// tests/Natoshare.Api.IntegrationTests/AdminAppTests.cs against the real API).
test("an admin can log in and see the users list, a user's detail, and the audit log", async ({ page }) => {
  await page.goto("/login");
  await page.fill("#email", process.env.E2E_ADMIN_EMAIL ?? "admin@natoshare.dev");
  await page.fill("#password", process.env.E2E_ADMIN_PASSWORD ?? "NatoshareAdmin1");
  await page.click('button[type="submit"]');

  await page.waitForURL("**/dashboard", { timeout: 15000 });
  await expect(page.getByText("Total users")).toBeVisible();

  await page.getByRole("link", { name: "Users" }).click();
  await page.waitForURL("**/users");
  const firstUserLink = page.locator("table tbody tr td a").first();
  await expect(firstUserLink).toBeVisible();

  await firstUserLink.click();
  await page.waitForURL(/\/users\/[0-9a-f-]+$/, { timeout: 15000 });
  await expect(page.getByText("Actions")).toBeVisible();

  await page.getByRole("link", { name: "Audit log" }).click();
  await page.waitForURL("**/audit");
  await expect(page.getByText(/events\.$/)).toBeVisible();
});
