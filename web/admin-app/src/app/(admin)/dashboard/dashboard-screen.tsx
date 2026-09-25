"use client";

import { useEffect, useState } from "react";
import type { AdminMetrics } from "@natoshare/shared-types";
import { Card } from "@/components/ui/card";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";

// A quick read on how the platform is doing: who is signing up, who is paying, and
// whether months are actually getting closed. See docs/04-admin-app.md section 2.1.
export function DashboardScreen() {
  const accessToken = useAdminAuthStore((state) => state.accessToken);
  const [metrics, setMetrics] = useState<AdminMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!accessToken) return;

    apiFetch<AdminMetrics>("/admin/metrics", { token: accessToken })
      .then((result) => {
        setMetrics(result);
        setError(null);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load metrics."));
  }, [accessToken]);

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-text">Dashboard</h1>
        <p className="mt-1 text-sm text-muted">A quick read on how Natoshare is doing right now.</p>
      </div>

      {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      {!metrics && !error && <p className="text-sm text-muted">Loading…</p>}

      {metrics && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
          <Tile label="Total users" value={metrics.totalUsers} />
          <Tile label="Signups (7 days)" value={metrics.signupsLast7Days} />
          <Tile label="Signups (30 days)" value={metrics.signupsLast30Days} />
          <Tile label="Active (30 days)" value={metrics.activeUsersLast30Days} />
          <Tile label="On trial" value={metrics.trialUsers} />
          <Tile label="Pro subscribers" value={metrics.proUsers} />
          <Tile label="Suspended" value={metrics.suspendedUsers} />
          <Tile label="Pending upgrades" value={metrics.pendingSubscriptions} />
          <Tile label="Month close rate (30 days)" value={`${Math.round(metrics.monthCloseRateLast30Days * 100)}%`} />
        </div>
      )}
    </div>
  );
}

function Tile({ label, value }: { label: string; value: string | number }) {
  return (
    <Card>
      <p className="text-sm text-muted">{label}</p>
      <p className="mt-1.5 font-display text-2xl font-bold text-text">{value}</p>
    </Card>
  );
}
