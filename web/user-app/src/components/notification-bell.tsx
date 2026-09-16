"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type { Notification } from "@natoshare/shared-types";
import { apiFetch } from "@/lib/api-client";

// A small bell in the header that shows how many notifications a user has not read
// yet, and links through to the full notification centre. Only ever shown once
// someone is logged in.
export function NotificationBell({ accessToken }: { accessToken: string | null }) {
  const [unreadCount, setUnreadCount] = useState(0);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    function refresh() {
      apiFetch<Notification[]>("/notifications?unreadOnly=true&pageSize=100", { token: accessToken })
        .then((notifications) => setUnreadCount(notifications.length))
        .catch(() => {});
    }

    refresh();

    // The bell does not remount just because something on the same page created a
    // new notification (logging an overspending expense, for example), so it has
    // to check back on its own instead of only ever reading the count once.
    const interval = setInterval(refresh, 30_000);
    return () => clearInterval(interval);
  }, [accessToken]);

  return (
    <Link href="/notifications" className="relative flex h-9 w-9 items-center justify-center rounded-full hover:bg-surface-2" aria-label="Notifications">
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" className="text-muted">
        <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
        <path d="M13.7 21a2 2 0 0 1-3.4 0" />
      </svg>
      {unreadCount > 0 && (
        <span className="absolute -top-0.5 -right-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-danger px-1 text-[10px] font-bold text-white">
          {unreadCount > 9 ? "9+" : unreadCount}
        </span>
      )}
    </Link>
  );
}
