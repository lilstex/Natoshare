"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useState } from "react";
import { BrandMark } from "@/components/brand-mark";
import { NotificationBell } from "@/components/notification-bell";
import {
  BarChartIcon,
  CloseIcon,
  GridIcon,
  LogOutIcon,
  MenuIcon,
  RepeatIcon,
  StarIcon,
  TagIcon,
  TrendingUpIcon,
  WalletIcon,
} from "@/components/ui/icons";
import { apiFetch } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

const LOGGED_IN_LINKS = [
  { href: "/dashboard", label: "Dashboard", icon: GridIcon },
  { href: "/categories", label: "Categories", icon: TagIcon },
  { href: "/people-money", label: "Loans & debts", icon: WalletIcon },
  { href: "/investments", label: "Investments", icon: TrendingUpIcon },
  { href: "/recurring", label: "Recurring", icon: RepeatIcon },
  { href: "/reports", label: "Reports", icon: BarChartIcon },
  { href: "/plans", label: "Plans", icon: StarIcon },
];

// The header on the public landing page. It needs to be a client component because
// it changes depending on whether you are logged in: a visitor sees "Log in" and
// "Get started", someone already logged in sees their name and a way to log out.
//
// Below the "lg" breakpoint (see docs/06-design-system.md section 4.1) the seven
// links plus bell, name and logout no longer fit in one row, so they collapse behind
// a hamburger button instead of wrapping into a mess (docs/feedback.md Phase B).
export function SiteHeader() {
  const router = useRouter();
  const pathname = usePathname();
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const refreshToken = useAuthStore((state) => state.refreshToken);
  const clearSession = useAuthStore((state) => state.clearSession);

  const [isMenuOpen, setIsMenuOpen] = useState(false);

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
    <header
      className="relative px-6 py-5 shadow-md sm:px-10"
      style={{ background: "var(--gradient-brand)" }}
    >
      <div className="flex items-center justify-between">
        <Link href="/" className="flex items-center gap-2.5">
          <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/15">
            <BrandMark size={22} />
          </span>
          <span className="font-display text-lg font-semibold text-white">Natoshare</span>
        </Link>

        {/* Full nav, shown once there is room for all of it. The logged-in nav has
            seven links plus the bell, name and logout, that only stops feeling
            cramped from xl (1280px) up, one step past the usual lg breakpoint. */}
        {user ? (
          <nav className="hidden items-center gap-4 xl:flex">
            <div className="flex items-center gap-1">
              {LOGGED_IN_LINKS.map((link) => {
                const isActive = pathname === link.href;
                return (
                  <Link
                    key={link.href}
                    href={link.href}
                    aria-current={isActive ? "page" : undefined}
                    className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                      isActive ? "bg-white/15 text-white" : "text-white/80 hover:bg-white/10 hover:text-white"
                    }`}
                  >
                    {link.label}
                  </Link>
                );
              })}
            </div>
            <NotificationBell accessToken={accessToken} />
            <span className="text-sm text-white/80">
              Hi, <span className="font-semibold text-white">{user.displayName}</span>
            </span>
            <button
              type="button"
              onClick={handleLogout}
              className="rounded-md px-3 py-1.5 text-sm font-medium text-white/80 transition-colors hover:bg-white/10 hover:text-white"
            >
              Log out
            </button>
          </nav>
        ) : (
          <nav className="hidden items-center gap-3 xl:flex">
            <Link
              href="/plans"
              className="rounded-md px-3 py-1.5 text-sm font-medium text-white/80 transition-colors hover:bg-white/10 hover:text-white"
            >
              Plans
            </Link>
            <Link
              href="/login"
              className="rounded-md px-3 py-1.5 text-sm font-medium text-white/80 transition-colors hover:bg-white/10 hover:text-white"
            >
              Log in
            </Link>
            <Link
              href="/signup"
              className="inline-flex h-9 items-center justify-center rounded-md bg-white px-4 text-sm font-semibold text-primary shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md"
            >
              Get started
            </Link>
          </nav>
        )}

        {/* Below xl: the bell (if logged in) stays visible, everything else moves
            into the hamburger drawer. */}
        <div className="flex items-center gap-3 xl:hidden">
          {user && <NotificationBell accessToken={accessToken} />}
          <button
            type="button"
            onClick={() => setIsMenuOpen((open) => !open)}
            aria-label={isMenuOpen ? "Close menu" : "Open menu"}
            aria-expanded={isMenuOpen}
            className="flex h-10 w-10 items-center justify-center rounded-md border border-white/30 bg-white/10 text-white hover:bg-white/20"
          >
            {isMenuOpen ? <CloseIcon /> : <MenuIcon />}
          </button>
        </div>
      </div>

      {isMenuOpen && (
        <nav className="absolute inset-x-4 top-full z-20 mt-2 overflow-hidden rounded-xl border border-border bg-surface shadow-lg sm:inset-x-8 xl:hidden">
          {user ? (
            <>
              <div
                className="flex items-center gap-3 px-4 py-4"
                style={{ background: "var(--gradient-brand)" }}
              >
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-white/20 font-display text-base font-bold text-white">
                  {user.displayName.charAt(0).toUpperCase()}
                </span>
                <div className="min-w-0">
                  <p className="truncate text-sm font-semibold text-white">{user.displayName}</p>
                  <p className="truncate text-xs text-white/75">{user.email}</p>
                </div>
              </div>

              <div className="flex flex-col gap-1 p-3">
                {LOGGED_IN_LINKS.map((link) => {
                  const isActive = pathname === link.href;
                  const Icon = link.icon;
                  return (
                    <Link
                      key={link.href}
                      href={link.href}
                      onClick={() => setIsMenuOpen(false)}
                      aria-current={isActive ? "page" : undefined}
                      className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors ${
                        isActive
                          ? "bg-gradient-to-r from-primary-tint to-surface-2 text-primary"
                          : "text-text hover:bg-surface-2"
                      }`}
                    >
                      <Icon size={18} className={isActive ? "text-primary" : "text-subtle"} />
                      {link.label}
                    </Link>
                  );
                })}

                <div className="my-1 border-t border-border" />

                <button
                  type="button"
                  onClick={handleLogout}
                  className="flex items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm font-medium text-danger hover:bg-danger-tint"
                >
                  <LogOutIcon size={18} />
                  Log out
                </button>
              </div>
            </>
          ) : (
            <div className="flex flex-col gap-1 p-3">
              <Link
                href="/plans"
                onClick={() => setIsMenuOpen(false)}
                className="rounded-lg px-3 py-2.5 text-sm font-medium text-text hover:bg-surface-2"
              >
                Plans
              </Link>
              <Link
                href="/login"
                onClick={() => setIsMenuOpen(false)}
                className="rounded-lg px-3 py-2.5 text-sm font-medium text-text hover:bg-surface-2"
              >
                Log in
              </Link>
              <Link
                href="/signup"
                onClick={() => setIsMenuOpen(false)}
                className="mt-1 inline-flex h-11 items-center justify-center rounded-md bg-gradient-to-r from-cta to-cta-hover px-4 text-sm font-semibold text-white shadow-sm hover:brightness-105"
              >
                Get started
              </Link>
            </div>
          )}
        </nav>
      )}
    </header>
  );
}
