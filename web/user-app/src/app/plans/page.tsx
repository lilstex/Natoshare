import type { Metadata } from "next";
import { PlansScreen } from "./plans-screen";

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "https://natoshare.example.com";

// A public pricing page, same SEO treatment as the landing page, anyone deciding
// whether to sign up should be able to find and read this without an account.
export const metadata: Metadata = {
  title: "Natoshare pricing — Free vs Pro",
  description:
    "See what Natoshare's Free and Pro plans include: budget categories, transaction history, sinking-fund savings, recurring items, and CSV/PDF export. Every new account starts with a 30-day Pro trial.",
  alternates: { canonical: `${SITE_URL}/plans` },
  openGraph: {
    title: "Natoshare pricing",
    description: "Free vs Pro, and a 30-day Pro trial on every new account.",
    url: `${SITE_URL}/plans`,
    siteName: "Natoshare",
    type: "website",
  },
};

export default function PlansPage() {
  return <PlansScreen />;
}
