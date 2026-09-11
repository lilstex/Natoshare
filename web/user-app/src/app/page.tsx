import { BrandMark } from "@/components/brand-mark";

// This is a placeholder home page for Phase 0. It just proves that our fonts, colors
// and layout are wired up correctly. The real landing page, with proper sections and
// copy, gets built in Phase 1.
export default function Home() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-6 px-6 text-center">
      <BrandMark size={64} />
      <h1 className="text-3xl font-bold text-text">Natoshare</h1>
      <p className="max-w-md text-muted">
        Split it. Track it. Keep it. We are still building this page, check back soon.
      </p>
    </main>
  );
}
