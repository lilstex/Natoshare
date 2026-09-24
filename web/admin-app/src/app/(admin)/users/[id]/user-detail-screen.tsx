"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import type { AdminUserDetail, RecomputeBalancesResult } from "@natoshare/shared-types";
import { Badge, statusTone } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { TextField } from "@/components/ui/text-field";
import { TypedConfirmDialog } from "@/components/ui/typed-confirm-dialog";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAdminAuthStore } from "@/store/auth-store";

// Everything the team can see and do for one account, docs/04-admin-app.md section
// 2.2. Every action button below calls an audited endpoint, none of them touch
// money, only account state (suspend, plan, and so on).
export function UserDetailScreen() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const accessToken = useAdminAuthStore((state) => state.accessToken);

  const [detail, setDetail] = useState<AdminUserDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [suspendReason, setSuspendReason] = useState("");
  const [showSuspendForm, setShowSuspendForm] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [planChoice, setPlanChoice] = useState<"Free" | "Pro">("Free");
  const [diff, setDiff] = useState<RecomputeBalancesResult | null>(null);

  const load = useCallback(() => {
    if (!accessToken) return;

    apiFetch<AdminUserDetail>(`/admin/users/${params.id}`, { token: accessToken })
      .then((data) => {
        setDetail(data);
        setPlanChoice(data.entitlements.plan);
        setError(null);
      })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Could not load this account."));
  }, [accessToken, params.id]);

  useEffect(() => {
    load();
  }, [load]);

  async function runAction(action: () => Promise<void>, successMessage: string) {
    if (!accessToken) return;
    setBusy(true);
    setError(null);
    try {
      await action();
      setNotice(successMessage);
      load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "That action did not work.");
    } finally {
      setBusy(false);
    }
  }

  if (error && !detail) {
    return <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>;
  }

  if (!detail) {
    return <p className="text-sm text-muted">Loading…</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-text">{detail.displayName}</h1>
          <p className="mt-1 text-sm text-muted">{detail.email}</p>
        </div>
        <Badge tone={statusTone(detail.status)}>{detail.status}</Badge>
      </div>

      {notice && <p className="rounded-xl bg-success-tint px-3.5 py-2.5 text-sm text-success">{notice}</p>}
      {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <h2 className="font-display text-base font-bold text-text">Profile</h2>
          <dl className="mt-3 grid grid-cols-2 gap-3 text-sm">
            <Field label="Role" value={detail.role} />
            <Field label="Currency" value={detail.currencyCode} />
            <Field label="Timezone" value={detail.timeZoneId} />
            <Field label="Joined" value={new Date(detail.createdAt).toLocaleDateString()} />
            <Field label="Trial ends" value={new Date(detail.trialEndsAt).toLocaleDateString()} />
            <Field label="Onboarded" value={detail.onboardingCompletedAt ? "Yes" : "Not yet"} />
          </dl>

          <h3 className="mt-5 text-sm font-semibold text-text">Plan</h3>
          <p className="mt-1 text-sm text-muted">
            {detail.entitlements.plan}
            {detail.entitlements.isTrial ? " (on trial)" : ""}, {detail.entitlements.maxCategories ?? "unlimited"} categories,{" "}
            {detail.entitlements.historyWindowDays ?? "unlimited"} days of history.
          </p>

          <h3 className="mt-5 text-sm font-semibold text-text">Current month</h3>
          <dl className="mt-2 grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
            <Field label="Allocated" value={detail.currentMonth.totals.allocated.toFixed(2)} />
            <Field label="Spent" value={detail.currentMonth.totals.spent.toFixed(2)} />
            <Field label="Available" value={detail.currentMonth.totals.available.toFixed(2)} />
            <Field label="Deficit" value={detail.currentMonth.totals.deficit.toFixed(2)} />
          </dl>

          <dl className="mt-5 grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
            <Field label="Deficit resolutions" value={String(detail.deficitResolutionCount)} />
            <Field label="Config versions" value={String(detail.configVersionCount)} />
            <Field label="Unread alerts" value={String(detail.unreadNotificationCount)} />
            <Field label="Alert preferences" value={String(detail.alertPreferenceCount)} />
          </dl>
        </Card>

        <Card className="flex flex-col gap-3">
          <h2 className="font-display text-base font-bold text-text">Actions</h2>

          {detail.status === "Active" && !showSuspendForm && (
            <Button onClick={() => setShowSuspendForm(true)} variant="danger">
              Suspend account
            </Button>
          )}

          {showSuspendForm && (
            <div className="flex flex-col gap-2 rounded-xl border border-border p-3">
              <TextField
                id="suspend-reason"
                label="Reason"
                value={suspendReason}
                onChange={(e) => setSuspendReason(e.target.value)}
              />
              <div className="flex gap-2">
                <Button
                  disabled={busy || !suspendReason.trim()}
                  variant="danger"
                  onClick={() =>
                    runAction(async () => {
                      await apiFetch(`/admin/users/${detail.id}/suspend`, {
                        method: "POST",
                        token: accessToken,
                        body: { reason: suspendReason },
                      });
                      setShowSuspendForm(false);
                      setSuspendReason("");
                    }, "Account suspended.")
                  }
                >
                  Confirm suspend
                </Button>
                <button
                  type="button"
                  onClick={() => setShowSuspendForm(false)}
                  className="rounded-full px-4 text-sm font-medium text-muted hover:bg-surface-2"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}

          {detail.status === "Suspended" && (
            <Button
              disabled={busy}
              onClick={() =>
                runAction(async () => {
                  await apiFetch(`/admin/users/${detail.id}/reactivate`, { method: "POST", token: accessToken });
                }, "Account reactivated.")
              }
            >
              Reactivate account
            </Button>
          )}

          <Button
            disabled={busy}
            variant="secondary"
            onClick={() =>
              runAction(async () => {
                const result = await apiFetch<{ resetToken: string }>(`/admin/users/${detail.id}/reset-password`, {
                  method: "POST",
                  token: accessToken,
                });
                setNotice(`Reset token (share with the user through your support channel): ${result.resetToken}`);
              }, "")
            }
          >
            Generate password reset token
          </Button>

          <div className="flex flex-col gap-2 rounded-xl border border-border p-3">
            <label className="flex flex-col gap-1.5 text-left text-sm">
              <span className="font-medium text-muted">Plan</span>
              <select
                className="h-11 rounded-xl border border-border-strong bg-surface px-3.5 text-[15px] text-text outline-none focus:border-primary focus:ring-4 focus:ring-primary-tint"
                value={planChoice}
                onChange={(e) => setPlanChoice(e.target.value as "Free" | "Pro")}
              >
                <option value="Free">Free</option>
                <option value="Pro">Pro</option>
              </select>
            </label>
            <Button
              disabled={busy}
              variant="secondary"
              onClick={() =>
                runAction(async () => {
                  await apiFetch(`/admin/users/${detail.id}/plan`, {
                    method: "PATCH",
                    token: accessToken,
                    body: { plan: planChoice, trialEndsAt: null },
                  });
                }, "Plan updated.")
              }
            >
              Apply plan
            </Button>
          </div>

          <Button
            disabled={busy}
            variant="secondary"
            onClick={() =>
              runAction(async () => {
                const result = await apiFetch<{ exportId: string }>(`/admin/users/${detail.id}/export`, {
                  method: "POST",
                  token: accessToken,
                });
                setNotice(`Export requested (id ${result.exportId}). It will show up in the normal export flow.`);
              }, "")
            }
          >
            Request data export
          </Button>

          <Button
            disabled={busy}
            variant="secondary"
            onClick={async () => {
              if (!accessToken) return;
              setBusy(true);
              try {
                const result = await apiFetch<RecomputeBalancesResult>(`/admin/users/${detail.id}/recompute-balances`, {
                  token: accessToken,
                });
                setDiff(result);
              } catch (err) {
                setError(err instanceof ApiError ? err.message : "Could not recompute balances.");
              } finally {
                setBusy(false);
              }
            }}
          >
            Recompute this month&apos;s balances
          </Button>

          <div className="mt-2 border-t border-border pt-3">
            <Button disabled={busy} variant="danger" className="w-full" onClick={() => setShowDeleteConfirm(true)}>
              Hard delete account
            </Button>
            <p className="mt-1.5 text-xs text-subtle">This is final. It removes everything, not just the login.</p>
          </div>
        </Card>
      </div>

      {diff && (
        <Card>
          <h2 className="font-display text-base font-bold text-text">
            Recomputed balances for {diff.year}-{String(diff.month).padStart(2, "0")}
          </h2>
          {diff.diffs.length === 0 ? (
            <p className="mt-2 text-sm text-muted">There is no open month to check yet.</p>
          ) : (
            <table className="mt-3 w-full text-left text-sm">
              <thead className="text-xs uppercase tracking-wide text-subtle">
                <tr>
                  <th className="py-1.5">Category</th>
                  <th className="py-1.5">Stored spent</th>
                  <th className="py-1.5">Recomputed spent</th>
                  <th className="py-1.5">Drift</th>
                </tr>
              </thead>
              <tbody>
                {diff.diffs.map((d) => (
                  <tr key={d.categoryId} className="border-t border-border">
                    <td className="py-1.5">{d.categoryName}</td>
                    <td className="py-1.5">{d.storedSpent.toFixed(2)}</td>
                    <td className="py-1.5">{d.recomputedSpent.toFixed(2)}</td>
                    <td className="py-1.5">{d.hasDrift ? <Badge tone="danger">Drift</Badge> : <Badge tone="success">OK</Badge>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}

      <Card>
        <h2 className="font-display text-base font-bold text-text">Subscription history</h2>
        {detail.subscriptionHistory.length === 0 ? (
          <p className="mt-2 text-sm text-muted">No subscription requests yet.</p>
        ) : (
          <table className="mt-3 w-full text-left text-sm">
            <thead className="text-xs uppercase tracking-wide text-subtle">
              <tr>
                <th className="py-1.5">Reference</th>
                <th className="py-1.5">Status</th>
                <th className="py-1.5">Billing</th>
                <th className="py-1.5">Requested</th>
                <th className="py-1.5">Period end</th>
              </tr>
            </thead>
            <tbody>
              {detail.subscriptionHistory.map((s) => (
                <tr key={s.id} className="border-t border-border">
                  <td className="py-1.5 font-mono text-xs">{s.reference}</td>
                  <td className="py-1.5">{s.status}</td>
                  <td className="py-1.5">{s.billingCycle}</td>
                  <td className="py-1.5">{new Date(s.requestedAt).toLocaleDateString()}</td>
                  <td className="py-1.5">{s.periodEnd ? new Date(s.periodEnd).toLocaleDateString() : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      <Card>
        <h2 className="font-display text-base font-bold text-text">Recent activity</h2>
        {detail.recentActivity.length === 0 ? (
          <p className="mt-2 text-sm text-muted">Nothing recorded yet.</p>
        ) : (
          <ul className="mt-3 flex flex-col gap-2 text-sm">
            {detail.recentActivity.map((a) => (
              <li key={a.id} className="border-t border-border pt-2 first:border-0 first:pt-0">
                <span className="font-medium text-text">{a.action}</span>{" "}
                <span className="text-subtle">by {a.actorRole}</span>{" "}
                <span className="text-subtle">· {new Date(a.createdAt).toLocaleString()}</span>
                {(a.before || a.after) && (
                  <p className="text-xs text-muted">
                    {a.before && <>before: {a.before} </>}
                    {a.after && <>after: {a.after}</>}
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
      </Card>

      {showDeleteConfirm && (
        <TypedConfirmDialog
          title="Hard delete this account"
          description="This removes the account and everything it owns for good. It cannot be undone."
          confirmText={detail.email}
          confirmLabel="Delete forever"
          isBusy={busy}
          onCancel={() => setShowDeleteConfirm(false)}
          onConfirm={() =>
            runAction(async () => {
              await apiFetch(`/admin/users/${detail.id}`, {
                method: "DELETE",
                token: accessToken,
                body: { confirmText: detail.email },
              });
              router.replace("/users");
            }, "Account deleted.")
          }
        />
      )}
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-subtle">{label}</dt>
      <dd className="text-text">{value}</dd>
    </div>
  );
}
