const { chromium } = require("playwright");

const OUT = "/tmp/claude-1000/-home-lilstex-Lilstex-Work-ShotNub-Solutions-Natoshare/21d79a96-1ac4-4fbc-90f5-bb8baa120604/scratchpad";

async function checkOverflow(page, label) {
  const overflow = await page.evaluate(() => {
    const docWidth = document.documentElement.scrollWidth;
    const winWidth = window.innerWidth;
    const offenders = [];
    document.querySelectorAll("body *").forEach((el) => {
      const r = el.getBoundingClientRect();
      if (r.right > winWidth + 1 && r.width > 0 && el.children.length <= 3) {
        offenders.push({
          tag: el.tagName,
          cls: (el.className || "").toString().slice(0, 90),
          right: Math.round(r.right),
          width: Math.round(r.width),
          text: (el.textContent || "").trim().slice(0, 40),
        });
      }
    });
    return { docWidth, winWidth, offenders: offenders.slice(0, 15) };
  });
  console.log(`\n=== ${label} === docWidth:${overflow.docWidth} winWidth:${overflow.winWidth}`);
  if (overflow.docWidth > overflow.winWidth + 1) {
    console.log("HORIZONTAL OVERFLOW DETECTED. Offenders:");
    console.log(JSON.stringify(overflow.offenders, null, 2));
  } else {
    console.log("no horizontal overflow");
  }
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 375, height: 812 } });

  const email = `mobiletest+${Date.now()}@natoshare.dev`;

  await page.goto("http://localhost:3000/signup", { waitUntil: "networkidle" });
  await page.fill("#displayName", "Mobile Test");
  await page.fill("#email", email);
  await page.fill("#password", "MobileTest123");
  await page.click('button[type="submit"]');
  await page.waitForURL(/onboarding/, { timeout: 15000 });
  await page.waitForTimeout(600);

  await checkOverflow(page, "onboarding step 1");
  await page.screenshot({ path: `${OUT}/onboarding-step1-mobile.png`, fullPage: true });

  await page.click('button:has-text("Continue")');
  await page.waitForTimeout(600);

  await page.fill("#income", "500000");
  // pick "Build my own" then add a second row so we see a real multi-row list
  await page.click('button:has-text("Build my own")');
  await page.waitForTimeout(200);
  const nameInputs = page.locator('input[placeholder="Category name"]');
  await nameInputs.first().fill("Rent");
  await page.click('button:has-text("+ Add category")');
  await page.waitForTimeout(200);
  await nameInputs.nth(1).fill("Feeding and groceries");
  await page.waitForTimeout(300);

  await checkOverflow(page, "onboarding step 2 (category rows)");
  await page.screenshot({ path: `${OUT}/onboarding-step2-mobile.png`, fullPage: true });

  await browser.close();
  console.log("\ndone");
})();
