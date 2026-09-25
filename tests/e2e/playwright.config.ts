import { defineConfig, devices } from "@playwright/test";

// Both frontends and the API are expected to already be up before this runs (see
// the README in this folder, and .github/workflows/ci.yml's "e2e" job): the API
// needs a real Postgres behind it, which is outside anything Playwright itself can
// start, so CI brings the whole stack up with `docker compose` first, same as a
// developer would locally, then just points these two projects at it.
const userAppUrl = process.env.USER_APP_URL ?? "http://localhost:3000";
const adminAppUrl = process.env.ADMIN_APP_URL ?? "http://localhost:3001";

export default defineConfig({
  testDir: "./specs",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? "github" : "list",
  use: {
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "user-app",
      testMatch: /user-flow\.spec\.ts/,
      use: { ...devices["Desktop Chrome"], baseURL: userAppUrl },
    },
    {
      name: "admin-app",
      testMatch: /admin-flow\.spec\.ts/,
      use: { ...devices["Desktop Chrome"], baseURL: adminAppUrl },
    },
  ],
});
