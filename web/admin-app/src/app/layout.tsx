import type { Metadata } from "next";
import { Sora, Plus_Jakarta_Sans } from "next/font/google";
import "./globals.css";

// Same two fonts as the user app, so both apps still feel like one product.
const sora = Sora({
  variable: "--font-sora",
  subsets: ["latin"],
  weight: ["600", "700"],
});

const jakarta = Plus_Jakarta_Sans({
  variable: "--font-jakarta",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
});

// The admin app is only for the Natoshare team, nobody outside should ever land here
// from a search engine. "noindex, nofollow" tells every search engine to stay away,
// on every single page, not just this one.
export const metadata: Metadata = {
  title: {
    default: "Natoshare Admin",
    template: "%s · Natoshare Admin",
  },
  description: "Internal admin tools for the Natoshare team.",
  robots: {
    index: false,
    follow: false,
  },
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${sora.variable} ${jakarta.variable} h-full antialiased`}>
      <body className="min-h-full flex flex-col font-sans">{children}</body>
    </html>
  );
}
