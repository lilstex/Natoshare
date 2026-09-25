"use client";

import { useEffect, useState } from "react";
import type { AdminPlanConfig, AdminSubscriptionRecord } from "@natoshare/shared-types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { TextField } from "@/components/ui/text-field";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";

// The subscriptions queue (who is waiting to be activated) and the plan config
// editor (what Free and Pro can each do), docs/04-admin-app.md section 2.3.
export function SubscriptionsScreen() {
  const accessToken = useAdminAuthStore((state) => state.accessToken);
  const [statusFilter, setStatusFilter] = useState("Pending");
  const [subscriptions, setSubscriptions] = useState<AdminSubscriptionRecord[]>([]);
  const [plans, setPlans] = useState<AdminPlanConfig[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function load() {
    if (!accessToken) return;

    const params = statusFilter ? `?status=${statusFilter}` : "";
    apiFetch<AdminSubscriptionRecord[]>(`/admin/subscriptions${params}`, { token: accessToken })
      .then(setSubscriptions)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load subscriptions."));

    apiFetch<AdminPlanConfig[]>("/admin/plans", { token: accessToken })
      .then(setPlans)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load plan configs."));
  }

  useEffect(load, [accessToken, statusFilter]);

  async function activate(reference: string) {
    if (!accessToken) return;
    setBusy(true);
    try {
      await apiFetch(`/admin/subscriptions/${reference}/activate`, {
        method: "POST",
        token: accessToken,
        body: { periodEnd: null },
      });
      setNotice(`${reference} activated.`);
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not activate that subscription.");
    } finally {
      setBusy(false);
    }
  }

  async function updatePlan(plan: AdminPlanConfig) {
    if (!accessToken) return;
    setBusy(true);
    try {
      await apiFetch(`/admin/plans/${plan.plan}`, {
        method: "PATCH",
        token: accessToken,
        body: {
          maxCategories: plan.maxCategories,
          historyWindowDays: plan.historyWindowDays,
          sinkingFund: plan.sinkingFund,
          deficitCoverFromSavings: plan.deficitCoverFromSavings,
          recurring: plan.recurring,
          export: plan.export,
        },
      });
      setNotice(`${plan.plan} plan updated.`);
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not update that plan.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold text-text">Subscriptions</h1>
        <p className="mt-1 text-sm text-muted">Payments are stubbed, upgrades wait here for an admin to activate them.</p>
      </div>

      {notice && <p className="rounded-xl bg-success-tint px-3.5 py-2.5 text-sm text-success">{notice}</p>}
      {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      <Card>
        <div className="flex items-center justify-between">
          <h2 className="font-display text-base font-bold text-text">Queue</h2>
          <select
            className="h-10 rounded-xl border border-border-strong bg-surface px-3 text-sm text-text outline-none focus:border-primary"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
          >
            <option value="Pending">Pending</option>
            <option value="Active">Active</option>
            <option value="Expired">Expired</option>
            <option value="Cancelled">Cancelled</option>
            <option value="">All</option>
          </select>
        </div>

        {subscriptions.length === 0 ? (
          <p className="mt-3 text-sm text-muted">Nothing here.</p>
        ) : (
          <table className="mt-3 w-full text-left text-sm">
            <thead className="text-xs uppercase tracking-wide text-subtle">
              <tr>
                <th className="py-1.5">User</th>
                <th className="py-1.5">Reference</th>
                <th className="py-1.5">Status</th>
                <th className="py-1.5">Requested</th>
                <th className="py-1.5" />
              </tr>
            </thead>
            <tbody>
              {subscriptions.map((s) => (
                <tr key={s.id} className="border-t border-border">
                  <td className="py-1.5">
                    {s.userDisplayName} <span className="text-subtle">({s.userEmail})</span>
                  </td>
                  <td className="py-1.5 font-mono text-xs">{s.reference}</td>
                  <td className="py-1.5">
                    <Badge tone={s.status === "Active" ? "success" : s.status === "Pending" ? "warning" : "neutral"}>
                      {s.status}
                    </Badge>
                  </td>
                  <td className="py-1.5">{new Date(s.requestedAt).toLocaleDateString()}</td>
                  <td className="py-1.5">
                    {s.status === "Pending" && (
                      <Button className="h-8 px-3 text-xs" disabled={busy} onClick={() => activate(s.reference)}>
                        Activate
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      <Card>
        <h2 className="font-display text-base font-bold text-text">Plan configuration</h2>
        <div className="mt-3 grid gap-4 sm:grid-cols-2">
          {plans.map((plan) => (
            <PlanEditor key={plan.plan} plan={plan} busy={busy} onSave={updatePlan} />
          ))}
        </div>
      </Card>
    </div>
  );
}

function PlanEditor({ plan, busy, onSave }: { plan: AdminPlanConfig; busy: boolean; onSave: (plan: AdminPlanConfig) => void }) {
  const [draft, setDraft] = useState(plan);

  // Resyncs the editable draft whenever a fresh plan config comes back from the
  // server (after Save, or the periodic reload), not on every render, so typing in
  // the fields below is not clobbered mid-edit.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => setDraft(plan), [plan]);

  return (
    <div className="rounded-xl border border-border p-4">
      <h3 className="font-semibold text-text">{draft.plan}</h3>
      <div className="mt-3 flex flex-col gap-3">
        <TextField
          id={`${plan.plan}-max-categories`}
          label="Max categories (blank = unlimited)"
          type="number"
          value={draft.maxCategories ?? ""}
          onChange={(e) => setDraft({ ...draft, maxCategories: e.target.value ? Number(e.target.value) : null })}
        />
        <TextField
          id={`${plan.plan}-history-days`}
          label="History window (days, blank = unlimited)"
          type="number"
          value={draft.historyWindowDays ?? ""}
          onChange={(e) => setDraft({ ...draft, historyWindowDays: e.target.value ? Number(e.target.value) : null })}
        />
        {(
          [
            ["sinkingFund", "Sinking fund"],
            ["deficitCoverFromSavings", "Cover deficit from savings"],
            ["recurring", "Recurring items"],
            ["export", "Data export"],
          ] as const
        ).map(([key, label]) => (
          <label key={key} className="flex items-center gap-2 text-sm text-text">
            <input
              type="checkbox"
              checked={draft[key]}
              onChange={(e) => setDraft({ ...draft, [key]: e.target.checked })}
            />
            {label}
          </label>
        ))}
        <Button disabled={busy} className="h-9 self-start px-4 text-sm" onClick={() => onSave(draft)}>
          Save {draft.plan}
        </Button>
      </div>
    </div>
  );
}
