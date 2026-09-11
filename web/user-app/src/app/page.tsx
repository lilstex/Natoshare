import type { Metadata } from "next";
import Link from "next/link";
import { SiteHeader } from "@/components/site-header";

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "https://natoshare.example.com";

// This is the page search engines and new visitors see first, so it gets the full SEO
// treatment: a real title and description, Open Graph tags for link previews, and
// JSON-LD so search engines understand what Natoshare actually is. There is no real
// share image yet, so we leave that out rather than pointing at a file that does not
// exist.
export const metadata: Metadata = {
  title: "Natoshare — split your income, track your spending, keep what you save",
  description:
    "Natoshare splits your income into categories automatically, warns you before you overspend, and shows you what you saved each month. Works in any currency.",
  alternates: { canonical: SITE_URL },
  openGraph: {
    title: "Natoshare",
    description:
      "Split your income into categories automatically, track your spending, and see what you saved each month.",
    url: SITE_URL,
    siteName: "Natoshare",
    type: "website",
  },
  twitter: {
    card: "summary",
    title: "Natoshare",
    description: "Split your income, track your spending, keep what you save.",
  },
};

const DEFAULT_CATEGORIES = [
  { name: "Rent", percentage: 25, colorVar: "--cat-rent" },
  { name: "Feeding", percentage: 25, colorVar: "--cat-feeding" },
  { name: "Transportation", percentage: 15, colorVar: "--cat-transport" },
  { name: "Utility", percentage: 10, colorVar: "--cat-utility" },
  { name: "Subscription", percentage: 5, colorVar: "--cat-subs" },
  { name: "Investment", percentage: 20, colorVar: "--cat-invest" },
];

const HOW_IT_WORKS = [
  {
    title: "Set your income",
    body: "Tell Natoshare the income you can rely on every month, and pick your categories, or start from a template.",
  },
  {
    title: "Log income, it splits itself",
    body: "Every time you log that income, Natoshare divides it across your categories by the percentages you chose.",
  },
  {
    title: "Spend, and see where you stand",
    body: "Log your expenses against a category. Natoshare tells you if you are on track, before the month runs out.",
  },
];

export default function LandingPage() {
  const organizationSchema = {
    "@context": "https://schema.org",
    "@type": "SoftwareApplication",
    name: "Natoshare",
    applicationCategory: "FinanceApplication",
    operatingSystem: "Web",
    description:
      "Natoshare splits your income into categories automatically, tracks your spending, and shows you what you saved each month.",
    offers: {
      "@type": "Offer",
      price: "0",
      priceCurrency: "USD",
    },
  };

  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationSchema) }}
      />

      <SiteHeader />

      <main>
        <section className="mx-auto flex max-w-3xl flex-col items-center gap-6 px-6 pt-16 pb-20 text-center sm:pt-24">
          <h1 className="font-display text-4xl font-bold tracking-tight text-text sm:text-5xl">
            Split it. Track it. Keep it.
          </h1>
          <p className="max-w-xl text-lg text-muted">
            Natoshare splits your income into categories automatically, warns you before you overspend,
            and shows you exactly what you saved each month. In any currency.
          </p>
          <div className="flex flex-wrap items-center justify-center gap-3">
            <Link
              href="/signup"
              className="inline-flex h-12 items-center justify-center rounded-full bg-primary px-7 text-base font-semibold text-white hover:bg-primary-strong"
            >
              Start your free trial
            </Link>
            <Link
              href="/login"
              className="inline-flex h-12 items-center justify-center rounded-full border border-border-strong px-7 text-base font-semibold text-text hover:bg-surface-2"
            >
              Log in
            </Link>
          </div>
          <p className="text-xs text-subtle">No card needed. 30 days on us. We never hold your money.</p>
        </section>

        <section className="mx-auto max-w-5xl px-6 pb-20">
          <div
            className="rounded-[28px] p-8 text-white shadow-lg sm:p-10"
            style={{ background: "var(--gradient-brand)" }}
          >
            <p className="text-xs font-semibold tracking-wide text-white/70 uppercase">Net position</p>
            <p className="mt-2 font-display text-3xl font-bold">₦2,142,300</p>
            <div className="mt-6 flex flex-wrap gap-x-6 gap-y-2 text-sm text-white/85">
              <span>Savings ₦1.62M</span>
              <span>Deployed ₦480k</span>
              <span>Flexible Pool ₦42k</span>
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-5xl px-6 pb-20">
          <h2 className="text-center font-display text-2xl font-bold text-text sm:text-3xl">
            How it works
          </h2>
          <div className="mt-10 grid gap-6 sm:grid-cols-3">
            {HOW_IT_WORKS.map((step, index) => (
              <div key={step.title} className="rounded-3xl border border-border bg-surface p-6">
                <span className="font-display text-sm font-bold text-primary">0{index + 1}</span>
                <h3 className="mt-2 text-lg font-semibold text-text">{step.title}</h3>
                <p className="mt-2 text-sm text-muted">{step.body}</p>
              </div>
            ))}
          </div>
        </section>

        <section className="mx-auto max-w-5xl px-6 pb-20">
          <h2 className="text-center font-display text-2xl font-bold text-text sm:text-3xl">
            Categories built for real life
          </h2>
          <p className="mx-auto mt-2 max-w-xl text-center text-sm text-muted">
            Natoshare starts you off with a sensible default split. Rename, add, or remove categories any
            time, they just need to add up to 100%.
          </p>
          <div className="mt-8 flex flex-wrap justify-center gap-3">
            {DEFAULT_CATEGORIES.map((category) => (
              <span
                key={category.name}
                className="inline-flex items-center gap-2 rounded-full bg-surface-2 px-4 py-2 text-sm font-semibold text-text"
              >
                <span
                  className="h-2.5 w-2.5 rounded-full"
                  style={{ backgroundColor: `var(${category.colorVar})` }}
                />
                {category.name}
                <span className="text-muted">{category.percentage}%</span>
              </span>
            ))}
          </div>
        </section>

        <section className="mx-auto max-w-4xl px-6 pb-24">
          <h2 className="text-center font-display text-2xl font-bold text-text sm:text-3xl">
            Free to start, upgrade when you are ready
          </h2>
          <div className="mt-8 grid gap-6 sm:grid-cols-2">
            <div className="rounded-3xl border border-border bg-surface p-7">
              <h3 className="text-lg font-semibold text-text">Free</h3>
              <p className="mt-1 text-sm text-muted">Up to 4 categories, this month&apos;s numbers.</p>
            </div>
            <div className="rounded-3xl border border-primary bg-primary-tint p-7">
              <h3 className="text-lg font-semibold text-text">Pro</h3>
              <p className="mt-1 text-sm text-muted">
                Unlimited categories, savings that carry over, recurring items, and exports.
              </p>
            </div>
          </div>
          <p className="mt-6 text-center text-sm text-muted">
            Every new account gets 30 days of Pro to try everything.
          </p>
        </section>
      </main>

      <footer className="border-t border-border px-6 py-8 text-center text-sm text-subtle">
        Natoshare, by ShotNub Solutions, Lagos.
      </footer>
    </>
  );
}
