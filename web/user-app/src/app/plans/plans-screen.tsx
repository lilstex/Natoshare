"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type { PlanEntitlements, SubscriptionStatus, UpgradeResult } from "@natoshare/shared-types";
import { SiteHeader } from "@/components/site-header";
import { Button } from "@/components/ui/button";
import { CheckIcon, CloseIcon } from "@/components/ui/icons";
import { apiFetch, ApiError } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";

// Rows straight from docs/00-plan.md section 5's capability table, the exact
// numbers (category count, history window) live in PlanConfig and can be tuned by
// an admin, this table is just how they read today.
const ROWS: { label: string; free: string; pro: string }[] = [
  { label: "Budget categories", free: "4", pro: "Unlimited" },
  { label: "Transaction history window", free: "60 days", pro: "Full" },
  { label: "Sinking-fund carry-over", free: "✗", pro: "✓" },
  { label: "Cover a deficit from savings", free: "✗ (pool / carry-forward only)", pro: "✓" },
  { label: "Recurring items", free: "✗", pro: "✓" },
  { label: "CSV / PDF export", free: "✗", pro: "✓" },
  { label: "Obligations calendar", free: "Basic (7 days)", pro: "Full" },
  { label: "Loans / debts / promises", free: "✓", pro: "✓" },
  { label: "Pacing, safe-to-spend, deficit tracking", free: "✓", pro: "✓" },
];

// A cell that starts with "✓"/"✗" gets a real coloured icon instead of the plain
// character, everything else (a number, "Full", "Basic (7 days)") stays plain text.
function PlanCell({ value }: { value: string }) {
  if (value.startsWith("✓")) {
    return (
      <span className="inline-flex items-center gap-1.5">
        <CheckIcon size={16} className="text-success" />
        {value.slice(1).trim()}
      </span>
    );
  }

  if (value.startsWith("✗")) {
    return (
      <span className="inline-flex items-center gap-1.5">
        <CloseIcon size={13} className="text-subtle" />
        {value.slice(1).trim()}
      </span>
    );
  }

  return <>{value}</>;
}

export function PlansScreen() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);

  const [entitlements, setEntitlements] = useState<PlanEntitlements | null>(null);
  const [subscriptionStatus, setSubscriptionStatus] = useState<SubscriptionStatus | null>(null);
  const [upgradeResult, setUpgradeResult] = useState<UpgradeResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    Promise.all([
      apiFetch<PlanEntitlements>("/me/entitlements", { token: accessToken }),
      apiFetch<SubscriptionStatus>("/subscription/status", { token: accessToken }),
    ])
      .then(([entitlementsResult, statusResult]) => {
        setEntitlements(entitlementsResult);
        setSubscriptionStatus(statusResult);
      })
      .catch(() => {});
  }, [accessToken]);

  async function handleUpgrade() {
    setIsSubmitting(true);
    setError(null);

    try {
      const result = await apiFetch<UpgradeResult>("/subscription/upgrade", {
        method: "POST",
        token: accessToken,
        body: { plan: "Pro", billingCycle: "monthly" },
      });
      setUpgradeResult(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not start that upgrade.");
    } finally {
      setIsSubmitting(false);
    }
  }

  const isAlreadyPro = entitlements?.plan === "Pro" && !entitlements.isTrial;
  const hasPendingUpgrade = subscriptionStatus?.status === "Pending";

  return (
    <div>
      <SiteHeader />

      <main className="px-4 pb-24 pt-10 sm:px-6 lg:px-10 xl:px-16 2xl:px-24">
        <div className="text-center">
          <h1 className="font-display text-3xl font-bold text-text sm:text-4xl">Simple pricing</h1>
          <p className="mt-3 text-lg text-muted">Every new account starts with a 30-day Pro trial, no card needed.</p>
        </div>

        {user && entitlements?.isTrial && (
          <p className="mt-6 rounded-xl bg-primary-tint px-4 py-3 text-center text-sm text-primary">
            You are on your Pro trial until {new Date(entitlements.trialEndsAt).toLocaleDateString()}.
          </p>
        )}

        <div className="mt-10 overflow-x-auto rounded-2xl border border-border bg-gradient-to-br from-surface to-primary-tint">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-left">
                <th className="px-5 py-4 font-medium text-muted">Capability</th>
                <th className="px-5 py-4 font-display text-base font-bold text-text">Free</th>
                <th className="px-5 py-4 font-display text-base font-bold text-primary">Pro</th>
              </tr>
            </thead>
            <tbody>
              {ROWS.map((row) => (
                <tr key={row.label} className="border-b border-border last:border-0">
                  <td className="px-5 py-3 text-text">{row.label}</td>
                  <td className="px-5 py-3 text-muted">
                    <PlanCell value={row.free} />
                  </td>
                  <td className="px-5 py-3 font-medium text-text">
                    <PlanCell value={row.pro} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="mt-8 flex flex-col items-center gap-3">
          {error && <p className="rounded-xl bg-danger-tint px-3.5 py-2.5 text-sm text-danger">{error}</p>}

          {!user ? (
            <Link
              href="/signup"
              className="inline-flex h-12 items-center justify-center rounded-md bg-gradient-to-r from-cta to-cta-hover px-8 text-base font-semibold text-white shadow-md transition-all hover:-translate-y-0.5 hover:shadow-lg hover:brightness-105"
            >
              Start your free trial
            </Link>
          ) : upgradeResult || hasPendingUpgrade ? (
            <p className="rounded-xl bg-success-tint px-4 py-3 text-sm text-success">
              {upgradeResult?.message ?? "Your upgrade to Pro is pending, an admin will activate it."}
            </p>
          ) : isAlreadyPro ? (
            <p className="rounded-xl bg-success-tint px-4 py-3 text-sm text-success">You are already on Pro.</p>
          ) : (
            <Button type="button" onClick={handleUpgrade} disabled={isSubmitting}>
              {isSubmitting ? "Starting…" : "Upgrade to Pro"}
            </Button>
          )}

          <p className="text-xs text-subtle">
            Payments are not live yet, upgrading creates a request an admin activates by hand.
          </p>
        </div>
      </main>
    </div>
  );
}
