"use client";

import { useEffect, useState } from "react";
import type { AdminErrorLogEntry, AdminHealth, AdminJobs, IntegrityCheckStatus } from "@natoshare/shared-types";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { apiFetch, ApiError, API_URL } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";

// Is everything up, are the jobs running, is the ledger still balanced, and what
// has broken recently. See docs/04-admin-app.md section 2.5.
export function MonitoringScreen() {
  const accessToken = useAdminAuthStore((state) => state.accessToken);
  const [health, setHealth] = useState<AdminHealth | null>(null);
  const [jobs, setJobs] = useState<AdminJobs | null>(null);
  const [integrity, setIntegrity] = useState<IntegrityCheckStatus | null>(null);
  const [errors, setErrors] = useState<AdminErrorLogEntry[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [openingDashboard, setOpeningDashboard] = useState(false);

  useEffect(() => {
    if (!accessToken) return;

    apiFetch<AdminHealth>("/admin/health", { token: accessToken }).then(setHealth).catch(() => {});
    apiFetch<AdminJobs>("/admin/jobs", { token: accessToken }).then(setJobs).catch(() => {});
    apiFetch<IntegrityCheckStatus>("/admin/integrity-check", { token: accessToken }).then(setIntegrity).catch(() => {});
    apiFetch<AdminErrorLogEntry[]>("/admin/errors", { token: accessToken })
      .then(setErrors)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load monitoring data."));
  }, [accessToken]);

  // Mints a narrow, 2-minute dashboard-only token on click rather than reusing our
  // real session token in the link (see AdminOnlyDashboardAuthFilter and
  // 05-implementation-phases.md's Phase 11 security-review notes): a plain <a href>
  // has no way to attach an Authorization header, but it also should never have to
  // carry a full-privilege bearer token, since URLs end up in browser history,
  // proxy access logs and same-origin Referer headers.
  async function openHangfireDashboard() {
    if (!accessToken) return;
    setOpeningDashboard(true);
    try {
      const result = await apiFetch<{ token: string }>("/admin/hangfire-token", { token: accessToken });
      window.open(`${API_URL.replace(/\/api\/v1$/, "")}/hangfire?accessToken=${result.token}`, "_blank", "noopener,noreferrer");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not open the Hangfire dashboard.");
    } finally {
      setOpeningDashboard(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-text">Monitoring</h1>
        <p className="mt-1 text-sm text-muted">Is Natoshare healthy right now.</p>
      </div>

      {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <p className="text-sm text-muted">Database</p>
          <p className="mt-1.5"><Badge tone={health?.databaseStatus === "Healthy" ? "success" : "danger"}>{health?.databaseStatus ?? "…"}</Badge></p>
        </Card>
        <Card>
          <p className="text-sm text-muted">Hangfire</p>
          <p className="mt-1.5"><Badge tone={health?.hangfireStatus === "Healthy" ? "success" : "danger"}>{health?.hangfireStatus ?? "…"}</Badge></p>
        </Card>
        <Card>
          <p className="text-sm text-muted">Ledger integrity (last run)</p>
          <p className="mt-1.5">
            {integrity?.lastRanAt ? (
              <Badge tone={integrity.lastDriftCount === 0 ? "success" : "danger"}>
                {integrity.lastDriftCount === 0 ? "No drift" : `${integrity.lastDriftCount} drifted`}
              </Badge>
            ) : (
              <Badge tone="neutral">Never run</Badge>
            )}
          </p>
          {integrity?.lastRanAt && <p className="mt-1 text-xs text-subtle">{new Date(integrity.lastRanAt).toLocaleString()}</p>}
        </Card>
      </div>

      <Card>
        <div className="flex items-center justify-between">
          <h2 className="font-display text-base font-bold text-text">Background jobs</h2>
          <button
            type="button"
            onClick={openHangfireDashboard}
            disabled={openingDashboard}
            className="text-sm font-medium text-primary hover:underline disabled:opacity-50"
          >
            Open Hangfire dashboard ↗
          </button>
        </div>
        {jobs && (
          <div className="mt-3 grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Tile label="Enqueued" value={jobs.enqueuedCount} />
            <Tile label="Processing" value={jobs.processingCount} />
            <Tile label="Succeeded" value={jobs.succeededCount} />
            <Tile label="Failed" value={jobs.failedCount} />
          </div>
        )}

        {jobs && jobs.recentFailures.length > 0 && (
          <table className="mt-4 w-full text-left text-sm">
            <thead className="text-xs uppercase tracking-wide text-subtle">
              <tr>
                <th className="py-1.5">Job</th>
                <th className="py-1.5">Failed at</th>
                <th className="py-1.5">Error</th>
              </tr>
            </thead>
            <tbody>
              {jobs.recentFailures.map((failure) => (
                <tr key={failure.jobId} className="border-t border-border align-top">
                  <td className="py-1.5">{failure.jobName}</td>
                  <td className="py-1.5">{failure.failedAt ? new Date(failure.failedAt).toLocaleString() : "—"}</td>
                  <td className="py-1.5 text-danger">{failure.exceptionMessage ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      <Card>
        <h2 className="font-display text-base font-bold text-text">Recent errors</h2>
        <p className="mt-1 text-xs text-subtle">
          The last 100 error-level log lines. For deeper investigation, check the server logs directly.
        </p>
        {errors.length === 0 ? (
          <p className="mt-3 text-sm text-muted">No errors recorded since the API last restarted.</p>
        ) : (
          <ul className="mt-3 flex flex-col divide-y divide-border text-sm">
            {errors.map((entry, index) => (
              <li key={index} className="py-2">
                <p className="text-xs text-subtle">{new Date(entry.timestamp).toLocaleString()}</p>
                <p className="text-danger">{entry.message}</p>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

function Tile({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <p className="text-xs uppercase tracking-wide text-subtle">{label}</p>
      <p className="font-display text-xl font-bold text-text">{value}</p>
    </div>
  );
}
