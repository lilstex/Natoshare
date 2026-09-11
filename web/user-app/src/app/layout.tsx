import type { Metadata } from "next";
import { Sora, Plus_Jakarta_Sans } from "next/font/google";
import "./globals.css";

// Sora is our heading font. We only pull in the weights we actually use, so the
// page does not download font weights nobody sees.
const sora = Sora({
  variable: "--font-sora",
  subsets: ["latin"],
  weight: ["600", "700"],
});

// Plus Jakarta Sans is our body and UI font.
const jakarta = Plus_Jakarta_Sans({
  variable: "--font-jakarta",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
});

// This is the default title and description for every page in the app, unless a page
// sets its own with generateMetadata. The real landing page copy comes in Phase 1.
export const metadata: Metadata = {
  title: {
    default: "Natoshare",
    template: "%s · Natoshare",
  },
  description:
    "Natoshare helps you split your income into categories, track your spending, and see what you saved, in any currency.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${sora.variable} ${jakarta.variable} h-full antialiased`}>
      <body className="min-h-full flex flex-col font-sans">{children}</body>
    </html>
  );
}
