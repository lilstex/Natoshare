import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { RevealSection } from "@/components/reveal-section";
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
        <div className="relative overflow-hidden" style={{ background: "var(--gradient-brand)" }}>
          {/* A vivid, full-bleed gradient hero, per the 2026-09-24 visual refresh in
              docs/feedback.md: enticing rather than a plain white section. The
              decorative circles match the same treatment as the dashboard's own
              hero money card, so the two feel like one brand, not two designs. */}
          <div className="pointer-events-none absolute inset-0 overflow-hidden">
            <div className="absolute -top-24 -right-24 h-96 w-96 rounded-full bg-white/10" />
            <div className="absolute -bottom-32 -left-16 h-80 w-80 rounded-full bg-white/10" />
            <div className="absolute top-1/3 left-1/4 h-40 w-40 rounded-full bg-white/5" />
          </div>

          <section className="fade-in-up relative mx-auto flex max-w-3xl flex-col items-center gap-6 px-6 pt-16 pb-20 text-center sm:pt-24 md:pt-28">
            <h1 className="font-display text-4xl font-bold tracking-tight text-white sm:text-5xl md:text-6xl">
              Split it. Track it. Keep it.
            </h1>
            <p className="max-w-xl text-lg text-white/85 md:text-xl">
              Natoshare splits your income into categories automatically, warns you before you overspend,
              and shows you exactly what you saved each month. In any currency.
            </p>
            <div className="flex flex-wrap items-center justify-center gap-3">
              <Link
                href="/signup"
                className="inline-flex h-12 items-center justify-center rounded-md bg-white px-7 text-base font-semibold text-primary shadow-md transition-all hover:-translate-y-0.5 hover:shadow-lg"
              >
                Start your free trial
              </Link>
              <Link
                href="/login"
                className="inline-flex h-12 items-center justify-center rounded-md border border-white/40 bg-white/10 px-7 text-base font-semibold text-white shadow-sm backdrop-blur-sm transition-all hover:-translate-y-0.5 hover:bg-white/20"
              >
                Log in
              </Link>
            </div>
            <p className="text-xs text-white/70">No card needed. 30 days on us. We never hold your money.</p>
          </section>
        </div>

        <RevealSection className="mx-auto max-w-5xl px-6 pb-20 md:pb-28">
          <div className="mx-auto max-w-2xl text-center">
            <h2 className="font-display text-2xl font-bold text-text sm:text-3xl">
              See your whole financial picture
            </h2>
            <p className="mt-2 text-sm text-muted md:text-base">
              One dashboard: your net position, every category&apos;s pace, and what just happened, all in
              one place.
            </p>
          </div>
          <div className="mx-auto mt-10 max-w-4xl overflow-hidden rounded-[28px] border border-border shadow-lg">
            <Image
              src="/images/dashboard-mockup.svg"
              alt="The Natoshare dashboard, showing a net position card, category cards with pace bars, and recent activity"
              width={1040}
              height={700}
              className="h-auto w-full"
            />
          </div>
        </RevealSection>

        <RevealSection className="mx-auto max-w-5xl px-6 pb-20 md:pb-28">
          <div className="mx-auto max-w-2xl text-center">
            <h2 className="font-display text-2xl font-bold text-text sm:text-3xl">
              Reports that actually make sense
            </h2>
            <p className="mt-2 text-sm text-muted md:text-base">
              Allocated versus spent versus saved, per category, with a savings rate you can watch climb.
            </p>
          </div>
          <div className="mx-auto mt-10 max-w-4xl overflow-hidden rounded-[28px] border border-border shadow-lg">
            <Image
              src="/images/report-mockup.svg"
              alt="A Natoshare report, comparing allocated, spent and saved per category, next to a savings rate ring"
              width={1040}
              height={700}
              className="h-auto w-full"
            />
          </div>
        </RevealSection>

        <RevealSection className="mx-auto max-w-3xl px-6 pb-20 md:pb-28">
          <div className="mx-auto max-w-2xl text-center">
            <h2 className="font-display text-2xl font-bold text-text sm:text-3xl">
              Know exactly where it goes
            </h2>
            <p className="mt-2 text-sm text-muted md:text-base">
              A spending split at a glance, so &quot;where did it all go&quot; always has an answer.
            </p>
          </div>
          <div className="mx-auto mt-10 max-w-lg overflow-hidden rounded-[28px] border border-border shadow-lg">
            <Image
              src="/images/chart-mockup.svg"
              alt="A spending overview chart, split by category with a percentage legend"
              width={640}
              height={480}
              className="h-auto w-full"
            />
          </div>
        </RevealSection>

        <RevealSection className="mx-auto max-w-5xl px-6 pb-20 md:pb-28">
          <h2 className="text-center font-display text-2xl font-bold text-text sm:text-3xl">
            How it works
          </h2>
          <div className="mt-10 grid gap-6 sm:grid-cols-3">
            {HOW_IT_WORKS.map((step, index) => (
              <div
                key={step.title}
                className="rounded-3xl border border-border bg-gradient-to-br from-surface to-primary-tint p-6 shadow-sm transition-shadow hover:shadow-md"
              >
                <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-cta to-cta-hover font-display text-sm font-bold text-white">
                  {index + 1}
                </span>
                <h3 className="mt-3 text-lg font-semibold text-text">{step.title}</h3>
                <p className="mt-2 text-sm text-muted">{step.body}</p>
              </div>
            ))}
          </div>
        </RevealSection>

        <RevealSection className="mx-auto max-w-5xl px-6 pb-20 md:pb-28">
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
        </RevealSection>

        <RevealSection className="mx-auto max-w-4xl px-6 pb-24">
          <h2 className="text-center font-display text-2xl font-bold text-text sm:text-3xl">
            Free to start, upgrade when you are ready
          </h2>
          <div className="mt-8 grid gap-6 sm:grid-cols-2">
            <div className="rounded-3xl border border-border bg-gradient-to-br from-surface to-primary-tint p-7 shadow-sm">
              <h3 className="text-lg font-semibold text-text">Free</h3>
              <p className="mt-1 text-sm text-muted">Up to 4 categories, this month&apos;s numbers.</p>
            </div>
            <div
              className="rounded-3xl p-7 text-white shadow-lg"
              style={{ background: "var(--gradient-brand)" }}
            >
              <h3 className="text-lg font-semibold text-white">Pro</h3>
              <p className="mt-1 text-sm text-white/85">
                Unlimited categories, savings that carry over, recurring items, and exports.
              </p>
            </div>
          </div>
          <p className="mt-6 text-center text-sm text-muted">
            Every new account gets 30 days of Pro to try everything.
          </p>
        </RevealSection>
      </main>

      <footer className="border-t border-border px-6 py-8 text-center text-sm text-subtle">
        Natoshare, by ShotNub Solutions, Lagos.
      </footer>
    </>
  );
}
