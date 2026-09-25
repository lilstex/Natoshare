"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { BrandMark } from "@/components/brand-mark";
import { apiFetch } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";
import { useAdminGuard } from "@/lib/use-admin-guard";

const NAV_ITEMS = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/users", label: "Users" },
  { href: "/subscriptions", label: "Subscriptions" },
  { href: "/audit", label: "Audit log" },
  { href: "/monitoring", label: "Monitoring" },
  { href: "/settings", label: "Settings" },
];

// Wraps every authenticated admin screen: it redirects to /login if nobody is
// signed in (see useAdminGuard), and otherwise shows the sidebar and top bar every
// page shares. Pages under src/app/(admin) render inside this.
export function AdminShell({ children }: { children: React.ReactNode }) {
  const { user, ready, accessToken } = useAdminGuard();
  const pathname = usePathname();
  const router = useRouter();
  const clearSession = useAdminAuthStore((state) => state.clearSession);
  const refreshToken = useAdminAuthStore((state) => state.refreshToken);

  async function handleLogout() {
    try {
      await apiFetch("/auth/logout", { method: "POST", token: accessToken, body: { refreshToken } });
    } catch {
      // Being logged out on this device matters more than the server-side token
      // getting revoked right away, same reasoning the user app's header uses.
    }

    clearSession();
    router.replace("/login");
  }

  if (!ready) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-bg">
        <p className="text-sm text-muted">Loading…</p>
      </main>
    );
  }

  return (
    <div className="flex min-h-screen bg-bg">
      <aside className="flex w-60 shrink-0 flex-col gap-1 border-r border-border bg-surface px-4 pb-16 pt-6">
        <div className="mb-6 flex items-center gap-2.5 px-2">
          <BrandMark size={32} />
          <span className="font-display text-base font-semibold text-text">Natoshare Admin</span>
        </div>

        {NAV_ITEMS.map((item) => {
          const isActive = pathname === item.href || pathname.startsWith(`${item.href}/`);
          return (
            <Link
              key={item.href}
              href={item.href}
              className={`rounded-xl px-3.5 py-2.5 text-sm font-medium transition-colors ${
                isActive ? "bg-primary-tint text-primary" : "text-muted hover:bg-surface-2 hover:text-text"
              }`}
            >
              {item.label}
            </Link>
          );
        })}

        <div className="mt-auto border-t border-border pt-4">
          <p className="px-2 text-sm font-semibold text-text">{user?.displayName}</p>
          <p className="px-2 text-xs text-subtle">{user?.email}</p>
          <button
            type="button"
            onClick={handleLogout}
            className="mt-2 w-full rounded-xl px-2 py-2 text-left text-sm font-medium text-muted hover:bg-surface-2 hover:text-text"
          >
            Log out
          </button>
        </div>
      </aside>

      <main className="flex-1 overflow-y-auto px-8 py-8">{children}</main>
    </div>
  );
}
