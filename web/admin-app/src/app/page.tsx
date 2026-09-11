import { BrandMark } from "@/components/brand-mark";

// This is a placeholder home page for Phase 0. It just proves the admin app builds
// and picks up the same fonts and tokens as the user app. The real admin screens
// (users, audit log, monitoring, and so on) come in Phase 10.
export default function Home() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-6 px-6 text-center">
      <BrandMark size={64} />
      <h1 className="text-3xl font-bold text-text">Natoshare Admin</h1>
      <p className="max-w-md text-muted">This app is only for the Natoshare team. Admin screens are coming soon.</p>
    </main>
  );
}
