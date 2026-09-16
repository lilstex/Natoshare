"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { BrandMark } from "@/components/brand-mark";
import { NotificationBell } from "@/components/notification-bell";
import { apiFetch } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

// The header on the public landing page. It needs to be a client component because
// it changes depending on whether you are logged in: a visitor sees "Log in" and
// "Get started", someone already logged in sees their name and a way to log out.
export function SiteHeader() {
  const router = useRouter();
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const refreshToken = useAuthStore((state) => state.refreshToken);
  const clearSession = useAuthStore((state) => state.clearSession);

  async function handleLogout() {
    // We still clear the local session even if this call fails, being logged out on
    // this device matters more than the server-side token getting revoked right away.
    try {
      await apiFetch("/auth/logout", {
        method: "POST",
        token: accessToken,
        body: { refreshToken },
      });
    } catch {
      // Nothing to do here, see the comment above.
    }

    clearSession();
    router.refresh();
  }

  return (
    <header className="flex items-center justify-between px-6 py-5 sm:px-10">
      <Link href="/" className="flex items-center gap-2.5">
        <BrandMark size={30} />
        <span className="font-display text-lg font-semibold text-text">Natoshare</span>
      </Link>

      {user ? (
        <nav className="flex items-center gap-4">
          <Link href="/dashboard" className="text-sm font-medium text-muted hover:text-text">
            Dashboard
          </Link>
          <Link href="/categories" className="text-sm font-medium text-muted hover:text-text">
            Categories
          </Link>
          <NotificationBell accessToken={accessToken} />
          <span className="text-sm text-muted">
            Hi, <span className="font-semibold text-text">{user.displayName}</span>
          </span>
          <button
            type="button"
            onClick={handleLogout}
            className="text-sm font-medium text-muted hover:text-text"
          >
            Log out
          </button>
        </nav>
      ) : (
        <nav className="flex items-center gap-3">
          <Link href="/login" className="text-sm font-medium text-muted">
            Log in
          </Link>
          <Link
            href="/signup"
            className="inline-flex h-9 items-center justify-center rounded-full bg-primary px-4 text-sm font-semibold text-white hover:bg-primary-strong"
          >
            Get started
          </Link>
        </nav>
      )}
    </header>
  );
}
