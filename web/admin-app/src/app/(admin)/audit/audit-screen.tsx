"use client";

import { useEffect, useState } from "react";
import type { AdminAuditSearchResult } from "@natoshare/shared-types";
import { Card } from "@/components/ui/card";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";

// Read-only, docs/04-admin-app.md section 2.4: an audit log you can edit is not an
// audit log. Every admin action (and plenty of user actions like logins) end up
// here with a before/after line where one makes sense.
export function AuditScreen() {
  const accessToken = useAdminAuthStore((state) => state.accessToken);
  const [entityType, setEntityType] = useState("");
  const [action, setAction] = useState("");
  const [result, setResult] = useState<AdminAuditSearchResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);

  useEffect(() => {
    if (!accessToken) return;

    const params = new URLSearchParams({ page: String(page), pageSize: "50" });
    if (entityType) params.set("entityType", entityType);
    if (action) params.set("action", action);

    apiFetch<AdminAuditSearchResult>(`/admin/audit?${params.toString()}`, { token: accessToken })
      .then((data) => {
        setResult(data);
        setError(null);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load the audit log."));
  }, [accessToken, entityType, action, page]);

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-text">Audit log</h1>
        <p className="mt-1 text-sm text-muted">{result?.totalCount ?? 0} events.</p>
      </div>

      <Card className="flex flex-wrap gap-4">
        <div className="w-52">
          <TextField
            id="entity-type"
            label="Entity type"
            placeholder="User, PlanConfig…"
            value={entityType}
            onChange={(e) => {
              setPage(1);
              setEntityType(e.target.value);
            }}
          />
        </div>
        <div className="w-52">
          <TextField
            id="action"
            label="Action"
            placeholder="UserSuspended…"
            value={action}
            onChange={(e) => {
              setPage(1);
              setAction(e.target.value);
            }}
          />
        </div>
      </Card>

      {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      <Card className="flex flex-col divide-y divide-border p-0">
        {result?.items.map((event) => (
          <div key={event.id} className="px-5 py-3 text-sm">
            <div className="flex items-center justify-between">
              <span className="font-semibold text-text">{event.action}</span>
              <span className="text-xs text-subtle">{new Date(event.createdAt).toLocaleString()}</span>
            </div>
            <p className="text-muted">
              {event.actorRole} · {event.entityType} #{event.entityId} · ip {event.ip ?? "—"}
            </p>
            {(event.before || event.after) && (
              <div className="mt-1.5 grid gap-1 rounded-lg bg-surface-2 px-3 py-2 text-xs sm:grid-cols-2">
                <p>
                  <span className="font-semibold text-muted">Before:</span> {event.before ?? "—"}
                </p>
                <p>
                  <span className="font-semibold text-muted">After:</span> {event.after ?? "—"}
                </p>
              </div>
            )}
          </div>
        ))}
        {result?.items.length === 0 && <p className="px-5 py-4 text-sm text-muted">No events match this filter.</p>}
      </Card>

      {result && result.totalCount > result.pageSize && (
        <div className="flex items-center gap-3">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
            className="rounded-full border border-border px-4 py-2 text-sm font-medium text-text disabled:opacity-40"
          >
            Previous
          </button>
          <span className="text-sm text-muted">Page {page}</span>
          <button
            type="button"
            disabled={page * result.pageSize >= result.totalCount}
            onClick={() => setPage((p) => p + 1)}
            className="rounded-full border border-border px-4 py-2 text-sm font-medium text-text disabled:opacity-40"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
